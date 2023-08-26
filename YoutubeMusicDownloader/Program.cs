using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using VideoLibrary;
using VideoLibrary.Exceptions;

namespace YoutubeMusicDownloader {
   internal class Program {
      /// <summary>
      /// One url per line
      /// </summary>
      static string urlsPath = "urls.txt";
      /// <summary>
      /// This is default value. User can override it with the flag
      /// </summary>
      static string videoSaveDirectory = "./Videos";
      /// <summary>
      /// This is default value. User can override it with the flag
      /// </summary>
      static string audioSaveDirectory = "./Audios";
      static bool shouldDeleteVideoFiles = true;
      static bool shouldDownloadVideos = true;
      /// <summary>
      /// Values are between 0 and 1. 0 means not started, 1 means completed.<br />
      /// This is parallel to <see cref="urlsPath"/>. For example, if the first url is completed, the first element of this list will be 1.
      /// </summary>
      static List<float> downloadStatuses = new List<float>();
      /// <summary>
      /// This is a parallel list to <see cref="downloadStatuses"/>
      /// </summary>
      static List<string> progressTextsVideo = new List<string>();
      static List<string> progressTextsAudio = new List<string>();
      static bool isRenderProgressTextCancelled = false;

      public static async Task Main(string[] args) {
         // Flags
         if (args.Length > 0) {
            for (int i = 0; i < args.Length; i++) {
               switch (args[i]) {
                  case "--help":
                  case "-h":
                     PrintHelp();
                     return;
                  case "-u":
                     urlsPath = args[i + 1];
                     i++;
                     break;
                  case "-v":
                     videoSaveDirectory = args[i + 1];
                     i++;
                     break;
                  case "-a":
                     audioSaveDirectory = args[i + 1];
                     i++;
                     break;
                  case "--keep-videos":
                     shouldDeleteVideoFiles = false;
                     break;
                  case "--no-download":
                     shouldDownloadVideos = false;
                     break;
                  default:
                     Console.WriteLine($"ERROR: Invalid flag: {args[i]}");
                     PrintHelp();
                     return;
               }
            }
         }

         List<Task> tasks = new List<Task>();

         if (shouldDownloadVideos) {
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


            // Download videos parallelly
            for (int i = 0; i < urls.Count; i++) {
               downloadStatuses.Add(0);
               progressTextsVideo.Add($"[{new string('.', 50)}] 0.00%");

               Task videoTask = DownloadVideo(urls[i], videoSaveDirectory, i);
               tasks.Add(videoTask);
            }
         }

         Task @render = RenderProgressTextAsync(progressTextsVideo);

         // Wait for all the videos to be downloaded
         await Task.WhenAll(tasks);
         tasks.Clear();

         // Convert videos to audio parallelly
         var videoFiles = Directory.GetFiles(videoSaveDirectory, "*.mp4");
         for (int i = 0; i < videoFiles.Length; i++) {
            Task audioTask = VideoToAudio(videoFiles[i], audioSaveDirectory, i);
            tasks.Add(audioTask);

            progressTextsAudio.Add($"[{new string('.', 50)}] 0.00%");
         }

         isRenderProgressTextCancelled = true;
         await @render;
         isRenderProgressTextCancelled = false;

         @render = RenderProgressTextAsync(progressTextsVideo, progressTextsAudio);

         // Wait for all the videos to be converted to audio
         await Task.WhenAll(tasks);
         tasks.Clear();

         await Task.Delay(100); // Waiting here 100ms because thats the gap in RenderProgressText. So final iteration will get up to date info about renderLists parameter.
         isRenderProgressTextCancelled = true;
         await @render;
         isRenderProgressTextCancelled = false;

         // Delete video files
         if (shouldDeleteVideoFiles) {
            DeleteVideoFiles(videoSaveDirectory);
         }

         Console.WriteLine("Succesfully finished");
         //Console.ReadLine();
      }

      async static Task DownloadVideo(string url, string saveDirectoryPath, int index) {
         if (!Directory.Exists(saveDirectoryPath))
            Directory.CreateDirectory(saveDirectoryPath);

         // Download video
         var youtube = YouTube.Default;
         YouTubeVideo video;
         try {
            video = await youtube.GetVideoAsync(url);
         }
         catch (Exception e) when (e is UnavailableStreamException || e is ArgumentException) {
            lock (progressTextsVideo) {
               progressTextsVideo[index] = $"ERROR: Your url is invalid. Url order in urls file: {index}(zero-indexed). The url: {url}. Exception message: {e.Message}";
            }
            return;
         }
         using HttpClient client = new HttpClient();
         long totalByte;

         using Stream outputStream = File.OpenWrite($"{saveDirectoryPath}/{video.Title}.mp4");
         using var request = new HttpRequestMessage(HttpMethod.Head, video.Uri);

         Stream readStream;
         for (int i = 0; ; i++) {
            try {
               totalByte = (await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)).Content.Headers.ContentLength.Value;
               readStream = await client.GetStreamAsync(video.Uri);
               break;
            }
            catch (HttpRequestException e) {
               if (e.StatusCode == System.Net.HttpStatusCode.Forbidden && i < 8) continue;

               lock (progressTextsVideo) {
                  progressTextsVideo[index] = $"ERROR: Couldn't download {video.Title}. Exception message: {e.Message}";
               }
               outputStream.Dispose();
               outputStream.Close();
               File.Delete($"{saveDirectoryPath}/{video.Title}.mp4");
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
            downloadStatuses[index] = (float)totalRead / totalByte;
            float speed;
            try {
               speed = read / speedometer.ElapsedMilliseconds; // KB/s. decimal part is chopped off
            }
            catch (DivideByZeroException) {
               speed = 0;
            }
            lock (progressTextsVideo) {
               progressTextsVideo[index] = $"{(totalRead == totalByte ? "Downloaded" : "Downloading")} {speed,4}KB/s {GetProgressText(downloadStatuses[index], video.Title)}";
            }
         }
         readStream.Dispose();
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
      async static Task VideoToAudio(string videoPath, string audioSaveDirectoryPath, int index) {
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

         lock (progressTextsAudio) {
            progressTextsAudio[index] = $"Converted {GetProgressText(1, videoName)}";
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
         Console.WriteLine("Usage: yt2mp3 [-h | --help] [-u urlsPath] [-v videoSaveDirectory] [-a audioSaveDirectory] [--keep-videos] [--no-download]");
         Console.WriteLine("-h: Help");
         Console.WriteLine($"-u: Path to the urls file. Default is urls.txt. The format is one url per line.");
         Console.WriteLine($"-v: Path to the directory to save the videos. Default is {videoSaveDirectory}");
         Console.WriteLine($"-a: Path to the directory to save the audios. Default is {audioSaveDirectory}");
         Console.WriteLine("--keep-videos: Do not delete the video files after converting them to audio.");
         Console.WriteLine($"--no-download: Only convert from mp4 to mp3. If this flag is present urls.txt is ignored. User should provide video files. Default directory is ./Videos but it can be altered with the -v flag.");
         //Console.ReadLine();
      }
   }
}