param(
  [string]$Runtime = "win-x64",
  [string]$Configuration = "Release"
)

dotnet publish src\EdgeCalendar.App\EdgeCalendar.App.csproj `
  -c $Configuration `
  -r $Runtime `
  -p:PublishSingleFile=true `
  -p:SelfContained=true
