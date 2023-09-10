# yt2mp3  
This is a cli app to download musics from youtube without using youtube data api.  
It is really slow, average download speed ~40KB/s
## Quick start
Get binaries from release section.  
Put them inside of a folder included by your PATH environment variable.  
```cmd
yt2mp3 -p <youtube-url>
```
File will be downloaded to Downloads folder.
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
yt2mp3 [-h | --help] [-d audio | video] [-u <path-to-urls-file>] [-s <path-to-save-directory>] [--uri <url>] [-p <url>]
```
You can delete everything other than publish folder  
### Usage
```
yt2mp3 [-h | --help] [-d audio | video] [-u <path-to-urls-file>] [-s <path-to-save-directory>] [--uri <url>] [-p <url>]
-h: Print this help and exit program
-d, --download-mode: Download mode. Can be either audio or video. Default is audio.
-u, --urls-path: Path to urls file. Default is urls.txt. Format is one url per line. Urls in this file will be downloaded when program runs.
-s, --save-directory: Path to the directory to save the files. Default is Downloads folder
--uri: Gets the internal youtube uri of the video/audio of given url and exits program. -d must precede this flag if -d is used. So, in order to get video, type "yt2mp3 -d video --uri <url>"
-p, --plain: Downloads the given url. This url is the url that you see in search bar. If this flag is used, urls.txt(or whatever you provided with -u) file will be ignored.
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
The files will be downloaded to Downloads folder.
### Example 2
I want to download a video not audio.
```cmd
yt2mp3 -d video -p <url>
```
Or put your urls inside of urls.txt
#### urls.txt
```
https://www.youtube.com/watch?v=ZjBgEkbnX2I
https://www.youtube.com/watch?v=HeQX2HjkcNo
https://www.youtube.com/watch?v=094y1Z2wpJg
```
Then run
```cmd
yt2mp3 -d video
```
Videos will be downloaded to downloads folder.
