@echo off
cd /d d:\project\VoiceKassa
"src\VoiceKassa.Api\bin\Debug\net8.0\VoiceKassa.Api.exe" --environment Development --urls "http://localhost:55983" > "d:\project\VoiceKassa\_srv_out.txt" 2> "d:\project\VoiceKassa\_srv_err.txt"
