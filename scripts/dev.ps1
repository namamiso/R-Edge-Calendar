param(
  [ValidateSet("build","test","format")]
  [string]$Task = "build"
)

switch ($Task) {
  "build" { dotnet build }
  "test"  { dotnet test }
  "format" { dotnet format }
}
