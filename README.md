# yt2mp3  
This is a cli app to download musics from youtube without using youtube data api.
## Quick start
Get binaries from release section.  
Put them inside of a folder included by your PATH environment variable.  
Have a urls.txt file which you have youtube urls. One url per line.
```cmd
yt2mp3
```
Mp3 files wil be downloaded to ./Audios.
## How to run
You can use it directly typing dotnet run and you can pass the command line arguments like this
```cmd
dotnet run -- <command line arguments>
```
Note that you need to type this where .csproj file is.  
Or  
You can publish it like this
```cmd
dotnet publish -c Release
```
And add bin/Release/net7.0/publish to your PATH environment variable or take everything inside of publish folder and put them one of the folders included by PATH variable.  
And use it like this  
```
yt2mp3 [-h | --help] [-u urlsPath] [-v videoSaveDirectory] [-a audioSaveDirectory] [--keep-videos] [--no-download]
```
You can delete everything other than publish folder  
### Usage
```
yt2mp3 [-h | --help] [-u urlsPath] [-v videoSaveDirectory] [-a audioSaveDirectory] [--keep-videos] [--no-download]
-h: Help
-u: Path to the urls file. Default is urls.txt. The format is one url per line.
-v: Path to the directory to save the videos. Default is ./Videos
-a: Path to the directory to save the audios. Default is ./Audios
--keep-videos: Do not delete the video files after converting them to audio.
--no-download: Only convert from mp4 to mp3. If this flag is present urls.txt is ignored. User should provide video files. Default directory is ./Videos but it can be altered with the -v flag.
```
## Examples
### Example 1
I have 3 youtube urls inside of ./urls.txt  
#### urls.txt
```
https://www.youtube.com/watch?v=0CNPR2qNzxk 
https://www.youtube.com/watch?v=mBBrnaJVQvw 
https://www.youtube.com/watch?v=DHm4ueEwNRM
```
Then simply type
```cmd
yt2mp3
```
The mp3 files will be downloaded to ./Audios.
### Example 2
I have videos inside the folder ./Videos and want to convert them to mp3.
```cmd
yt2mp3 --no-download --keep-videos
```
If videos are in different folder then
```cmd
yt2mp3 --no-download --keep-videos -v <path-to-videos-folder>
```
