# DiscoTrack
Track artists and albums against your library. See what you've got and what you've yet to find.
## Build and test
```
dotnet build
dotnet test
```

## Setup
### Subsonic
```
dotnet user-secrets set "Subsonic:BaseUrl" "https://your-server"
dotnet user-secrets set "Subsonic:Username" "your-user"
dotnet user-secrets set "Subsonic:Salt" "your-salt"
dotnet user-secrets set "Subsonic:Token" "md5(password + salt)"
```

## Development
```
dotnet run --project DiscoTrack.Web
```
### Database
The database is created on first run.

Migrations are run automatically on each app start.

## TODO
- Handle library orphans correctly
