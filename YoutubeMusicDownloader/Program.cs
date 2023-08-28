using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoLibrary;
using VideoLibrary.Exceptions;

namespace YoutubeMusicDownloader {
   internal class Program {
      /// <summary>
      /// One url per line
      /// </summary>
      static string urlsPath = "urls.txt";
      static bool isRenderProgressTextCancelled = false;
      static DownloadMode downloadMode = DownloadMode.Audio;
      static string saveDirectory;
      /// <summary>
      /// Selects video or audio resource from the given url. Audio is selected by default.
      /// </summary>
      static Func<string, Task<YouTubeVideo>> GetResourceMethod = GetAudioWithHighestQuality;
      static volatile List<string> progressTextsDownload = new List<string>();

      public static async Task Main(string[] args) {
         // Set default save directory
         try {
            saveDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads"; // ~/Downloads

            if(!Directory.Exists(saveDirectory))
               saveDirectory = "./Downloads";
         }
         catch (Exception) {
            saveDirectory = "./Downloads";
         }

         // Flags
         for (int i = 0; i < args.Length; i++) {
            switch (args[i]) {
               case "--help":
               case "-h":
                  PrintHelp();
                  return;
               case "-d":
               case "--download-mode":
                  if (args[i + 1].ToLower() == "audio") {
                     downloadMode = DownloadMode.Audio;
                     GetResourceMethod = GetAudioWithHighestQuality;
                  } else if (args[i + 1].ToLower() == "video") {
                     downloadMode = DownloadMode.Video;
                     GetResourceMethod = GetVideoWithHighestQuality;
                  } else {
                     Console.WriteLine($"ERROR: Invalid download mode: {args[i + 1]}");
                     PrintHelp();
                     return;
                  }
                  i++;
                  break;
               case "-u":
               case "--urls-path":
                  urlsPath = args[i + 1];
                  i++;
                  break;
               case "-s":
               case "--save-directory":
                  saveDirectory = args[i + 1];
                  i++;
                  break;
               case "--uri":
                  var resource = downloadMode switch {
                     DownloadMode.Audio => await GetAudioWithHighestQuality(args[i + 1]),
                     DownloadMode.Video => await GetVideoWithHighestQuality(args[i + 1]),
                     DownloadMode.Unknown => null,
                     _ => null,
                  };
                  await Console.Out.WriteLineAsync($"{(resource == null ? "ERROR: Not a valid youtube url or youtube refuses to respond." : resource.Uri)}");
                  return;
               default:
                  Console.WriteLine($"ERROR: Invalid flag: {args[i]}");
                  PrintHelp();
                  return;
            }
         }

         List<YouTubeVideo> resources = new List<YouTubeVideo>();

         // If the urls file does not exist, say it to the user and exit.
         if (!File.Exists(urlsPath)) {
            Console.WriteLine($"ERROR: The urls file does not exist.");
            PrintHelp();
            return;
         }

         List<string> urls = new List<string>();

         // Read urls from file
         using (StreamReader sr = new StreamReader(urlsPath)) {
            string line;
            while ((line = await sr.ReadLineAsync()) != null) {
               if (!string.IsNullOrWhiteSpace(line))
                  urls.Add(line);
            }
         }
         
         // If the urls file is empty, say it to the user and exit.
         if (urls.Count == 0) {
            Console.WriteLine($"ERROR: The urls file is empty.");
            PrintHelp();
            return;
         }

         List<Task<YouTubeVideo>> getResourceTasks = new List<Task<YouTubeVideo>>();

         for (int i = 0; i < urls.Count; i++) {
            Task<YouTubeVideo> resourceTask = GetResourceMethod(urls[i]);
            getResourceTasks.Add(resourceTask);
         }

         await Task.WhenAll(getResourceTasks);
         for (int i = 0; i < getResourceTasks.Count; i++) {
            var resource = await getResourceTasks[i];
            if (resource == null) {
               lock (progressTextsDownload) { // Lock is not necessary but it is good practice.
                  progressTextsDownload[i] = $"ERROR: Your url is invalid or youtube refuses to respond. Url order in urls file: {i}(zero-indexed). The url: {urls[i]}.";
               }
            } else {
               resources.Add(resource);
            }
         }
         getResourceTasks.Clear();

         List<Task> downloadResourceTasks = new List<Task>();
         // Download resources parallelly
         for (int i = 0; i < resources.Count; i++) {
            progressTextsDownload.Add($"[{new string('.', 50)}] 0.00%");

            Task @download = DownloadYoutubeResource(resources[i], downloadMode, saveDirectory, i, progressTextsDownload);
            downloadResourceTasks.Add(@download);
         }

         Task @render = RenderProgressTextAsync(progressTextsDownload);

         // Wait for all the resources to be downloaded
         await Task.WhenAll(downloadResourceTasks);
         downloadResourceTasks.Clear();

         await Task.Delay(100); // Waiting here 100ms because thats the gap in RenderProgressText. So final iteration will get up to date info about renderLists parameter.
         isRenderProgressTextCancelled = true;
         await @render;
         isRenderProgressTextCancelled = false;

         Console.WriteLine("Succesfully finished");
      }

      async static Task DownloadYoutubeResource(YouTubeVideo resource, DownloadMode downloadMode, string saveDirectoryPath, int index, List<string> progressTexts) {
         if (!Directory.Exists(saveDirectoryPath))
            Directory.CreateDirectory(saveDirectoryPath);

         using HttpClient client = new HttpClient();
         long totalByte;
         string defaultFileExtension = downloadMode switch {
            DownloadMode.Audio => ".m4a",
            DownloadMode.Video => ".mp4",
            DownloadMode.Unknown => "",
            _ => "",
         };
         string outputFilePath = $"{saveDirectoryPath}/{RemoveInvalidFileNameChars(resource.Title)}{(string.IsNullOrEmpty(resource.FileExtension) ? defaultFileExtension : $"{resource.FileExtension}")}";

         using Stream outputStream = File.OpenWrite(outputFilePath);
         using var request = new HttpRequestMessage(HttpMethod.Head, resource.Uri);

         Stream readStream;
         for (int i = 0; ; i++) {
            try {
               totalByte = (await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)).Content.Headers.ContentLength.Value;
               readStream = await client.GetStreamAsync(resource.Uri);
               break;
            }
            catch (HttpRequestException e) {
               if (e.StatusCode == System.Net.HttpStatusCode.Forbidden && i < 8) continue;

               lock (progressTexts) {
                  progressTexts[index] = $"ERROR: Couldn't download {resource.Title}. Exception message: {e.Message}";
               }
               outputStream.Dispose();
               outputStream.Close();
               File.Delete(outputFilePath);
               return;
            }
         }
         
         byte[] buffer = new byte[128 * 1024]; // 128KB buffer
         int read;
         long totalRead = 0;

         Stopwatch speedometer = new Stopwatch();
         for (speedometer.Start(); (read = await readStream.ReadAsync(buffer)) > 0; speedometer.Restart()) {
            speedometer.Stop();
            await outputStream.WriteAsync(buffer.AsMemory(0, read));

            totalRead += read;
            float speed;
            try {
               speed = read / speedometer.ElapsedMilliseconds; // KB/s. decimal part is chopped off
            }
            catch (DivideByZeroException) {
               speed = 0;
            }
            lock (progressTexts) {
               progressTexts[index] = $"{(totalRead == totalByte ? "Downloaded" : "Downloading")} {speed,4}KB/s {GetProgressText((float)totalRead / totalByte, resource.Title)}";
            }
         }
         readStream.Dispose();
      }

      static string RemoveInvalidFileNameChars(string fileName) {
         string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
         Regex invalidCharRegex = new Regex($"[{Regex.Escape(invalidChars)}]");
         return invalidCharRegex.Replace(fileName, "_");
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="uri">This uri is the uri that shows up in the search bar.</param>
      /// <returns>null if url is invalid</returns>
      async static Task<YouTubeVideo> GetAudioWithHighestQuality(string uri) {
         var youtube = YouTube.Default;

         IEnumerable<YouTubeVideo> videos;
         try {
            videos = await youtube.GetAllVideosAsync(uri);
         }
         catch (Exception e) when (e is UnavailableStreamException || e is ArgumentException) {
            return null;
         }

         YouTubeVideo highestQualityAudio = null;
         try {
            highestQualityAudio = videos.OrderByDescending(v => v.AudioBitrate).First();
         } catch (InvalidOperationException) {
            // If there is no audio, it throws InvalidOperationException. So we catch it and do nothing.
         }

         return highestQualityAudio;
      }

      /// <summary>
      /// It gets highest quality video that has audio but there could be higher quality video without audio.
      /// </summary>
      /// <param name="uri">This uri is the uri that shows up in the search bar.</param>
      /// <returns>null if url is invalid</returns>
      async static Task<YouTubeVideo> GetVideoWithHighestQuality(string uri) {
         var youtube = YouTube.Default;

         IEnumerable<YouTubeVideo> videos;
         try {
            videos = await youtube.GetAllVideosAsync(uri);
         }
         catch (Exception e) when (e is UnavailableStreamException || e is ArgumentException) {
            return null;
         }

         YouTubeVideo highestQualityVideo = null;

         try {
            highestQualityVideo = videos.Where(v => v.Format == VideoFormat.Mp4 && v.AudioFormat != AudioFormat.Unknown).OrderByDescending(v => v.Resolution).First();
         }
         catch (InvalidOperationException) {
            // If there is no mp4 video, it throws InvalidOperationException. So we catch it and do nothing.
         }

         try {
            highestQualityVideo ??= videos.OrderByDescending(v => v.Resolution).First(v => v.AudioFormat != AudioFormat.Unknown);
         }
         catch (InvalidOperationException) {
            // If there is no mp4 video, it throws InvalidOperationException. So we catch it and do nothing.
         }


         return highestQualityVideo;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="progress">Normalized value</param>
      /// <param name="progressName"></param>
      /// <returns></returns>
      static string GetProgressText(float progress, string progressName) {
         float progressPercentage = progress * 100;
         string progressText = $"[{new string('#', (int)(progressPercentage / 2))}{new string('.', (int)(50 - progressPercentage / 2))}] {progressPercentage:F2}%  {progressName}";

         return progressText;
      }

      /// <summary>
      /// Renders the contents of <paramref name="renderLists"/> to the console every 100ms.
      /// </summary>
      /// <returns></returns>
      async static Task RenderProgressTextAsync(params IEnumerable<string>[] renderLists) {
         while (!isRenderProgressTextCancelled) {
            Console.Clear();
            foreach (var renderList in renderLists) {
               lock (renderList) {
                  foreach (var item in renderList) {
                     Console.WriteLine(item);
                  }
               }
            }
            await Task.Delay(100);
         }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="videoPath">With extension</param>
      /// <param name="audioSaveDirectoryPath"></param>
      /// <returns></returns>
      async static Task VideoToAudio(string videoPath, string audioSaveDirectoryPath, int index, List<string> progressTexts) {
         // Create audio output directory
         if (!Directory.Exists(audioSaveDirectoryPath))
            Directory.CreateDirectory(audioSaveDirectoryPath);
      
         string videoName = Path.GetFileNameWithoutExtension(videoPath);
      
         // Convert video to mp3
         var outputPath = $"{audioSaveDirectoryPath}/{videoName}.mp3";
         using FileStream outputStream = new FileStream(outputPath, FileMode.OpenOrCreate, FileAccess.Write);
      
         using var reader = new MediaFoundationReader(videoPath);
         using var writer = new LameMP3FileWriter(outputStream, reader.WaveFormat, 128);
      
         await reader.CopyToAsync(writer);
      
         lock (progressTexts) {
            progressTexts[index] = $"Converted {GetProgressText(1, videoName)}";
         }
      }


      /// <summary>
      /// Deletes all the video files in the given directory.
      /// Also deletes the directory itself if it is empty.
      /// </summary>
      /// <param name="directory"></param>
      /// <returns></returns>
      static void DeleteVideoFiles(string directory) {
         // Delete all .mp4 files in the videoPath directory
         string[] files = Directory.GetFiles(directory, "*.mp4");
         foreach (var file in files) {
            File.Delete(file);
            Console.WriteLine($"Deleted {Path.GetFileName(file)}");
         }
      
         try {
            Directory.Delete(directory);
            Console.WriteLine($"Deleted temporary video storage directory {directory}");
         }
         catch (IOException) {
            // The directory is not empty.
            // Do nothing ie. keep it as it is.
         }
      }

      static void PrintHelp() {
         Console.WriteLine("Usage: yt2mp3 [-h | --help] [-d audio | video] [-u <path-to-urls-file>] [-s <path-to-save-directory>] [--uri <url>]");
         Console.WriteLine("-h: Print this help and exit program");
         Console.WriteLine("-d, --download-mode: Download mode. Can be either audio or video. Default is audio.");
         Console.WriteLine("-u, --urls-path: Path to urls file. Default is urls.txt. Format is one url per line. Urls in this file will be downloaded when program runs.");
         Console.WriteLine("-s, --save-directory: Path to the directory to save the files. Default is Downloads folder");
         Console.WriteLine("--uri: Gets the internal youtube uri of the video/audio of given url and exits program. -d must precede this flag if -d is used otherwise -d has no effect. So, in order to get video type \"yt2mp3 -d video --uri <url>\"");
      }
   }

   public enum DownloadMode {
      Audio,
      Video,
      Unknown
   }
}