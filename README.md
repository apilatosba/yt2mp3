# yt2mp3  
This is a cli app to download musics from youtube without using youtube data api.  
It is really slow, average download speed ~40KB/s
## Quick start
Get binaries from release section.  
Put them inside of a folder included by your PATH environment variable.  
Have a urls.txt file which you have youtube urls. One url per line.
```cmd
yt2mp3
```
Files will be downloaded to Downloads folder.
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
yt2mp3 [-h | --help] [-d <audio | video>] [-u <path-to-urls-file>] [-s <path-to-save-directory>]
```
You can delete everything other than publish folder  
### Usage
```
yt2mp3 [-h | --help] [-d <audio | video>] [-u <path-to-urls-file>] [-s <path-to-save-directory>]
-h: Print this help
-d, --download-mode: Download mode. Can be either audio or video. Default is audio.
-u, --urls-path: Path to urls file. Default is urls.txt. Format is one url per line. Urls in this file will be downloaded when program runs.
-s, --save-directory: Path to the directory to save the files. Default is Downloads folder
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
