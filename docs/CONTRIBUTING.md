# Contributing to Cine

## Code of Conduct

Please be respectful and constructive in all interactions. This project follows the [Contributor Covenant](https://www.contributor-covenant.org/).

## Getting Started

1. Read [ARCHITECTURE.md](ARCHITECTURE.md) to understand the codebase structure
2. Read [BUILD.md](BUILD.md) for build prerequisites and instructions
3. Set up your development environment (see BUILD.md)

## Development Workflow

### 1. Branch Strategy

```
main        — Production releases
├── develop — Integration branch for next release
│   ├── feature/xxx — New features (branch from develop)
│   ├── fix/xxx     — Bug fixes (branch from develop)
│   └── refactor/xxx— Refactoring (branch from develop)
└── hotfix/xxx — Critical fixes (branch from main)
```

### 2. Before Starting Work

```powershell
git checkout develop
git pull origin develop
git checkout -b feature/your-feature-name
```

### 3. Code Style

#### C# Conventions

- **Naming**: `PascalCase` for public members, `_camelCase` for private fields, `camelCase` for locals
- **Nullable**: All projects have `<WarningsAsErrors>nullable</WarningsAsErrors>`
- **Braces**: Allman style (opening brace on new line)
- **File layout**: 1 type per file (except small records)
- **Partial files**: Grouped as `TypeName.Area.cs` (e.g., `MainViewModel.Video.cs`)
- **Regions**: Use `// ═══` separators for section breaks, no `#region` directives

#### XML Documentation

All public methods and properties must have `<summary>` comments:

```csharp
/// <summary>Opens a media file, updating all UI state.</summary>
/// <param name="path">Absolute path to the media file.</param>
public async void OpenFile(string path) { ... }
```

#### Service Patterns

- **Always define an interface** for services injected into ViewModels
- **Constructor injection** via `IServiceProvider` / Microsoft.Extensions.DependencyInjection
- **Optional dependencies** use nullable parameters with null-coalescing defaults
- **Async methods** return `Task<T>` or `Task`, not `async void` (except for event handlers)

### 4. Testing Requirements

- **All new services** must have corresponding unit tests in `tests/Cine.Tests/Services/`
- **All new managers** must have tests in `tests/Cine.Tests/Managers/`
- **UI changes** requiring Avalonia interaction use `[Collection("Headless")]` with `HeadlessFixture`
- **Pure logic** (no Avalonia dependency) uses plain xUnit + NSubstitute + Shouldly

#### Test patterns

```csharp
// Good: mock dependencies, test behavior
[Fact]
public void Add_DuplicatePath_ShouldNotAddTwice()
{
    var coordinator = new PlaylistCoordigator();
    coordinator.Add(@"C:\video.mp4");
    coordinator.Add(@"C:\video.mp4");
    Assert.Single(coordinator.Items);
}

// Good: verify round-trip persistence
[Fact]
public void SaveAndLoad_RoundTrip_ShouldPreserveOrder()
{
    // Arrange
    var store = new PlaylistSettingsStore(testPath);
    store.SavePlaylist(new[] { "a.mp4", "b.mp4" }, 0);

    // Act
    var result = store.LoadPlaylist(out int pos);

    // Assert
    result.ShouldBe([ "a.mp4", "b.mp4" ]);
    pos.ShouldBe(0);
}
```

### 5. Commit Guidelines

- Use conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`
- Keep commits focused: 1 logical change per commit
- Reference issues: `fix(#42): correct playlist shuffle off-by-one`
- Write in imperative present tense: "Add" not "Added", "Fix" not "Fixed"

### 6. Pull Request Process

1. Ensure all tests pass: `dotnet test tests/Cine.Tests/Cine.Tests.csproj`
2. Ensure no nullable warnings: `dotnet build -warnaserror`
3. Run benchmarks if your change affects performance: `dotnet run --project tests/Cine.Benchmarks/ -c Release`
4. Create PR against `develop` branch
5. Add a clear description of what changed and why
6. Add reviewer(s) from the maintainers list

### 7. Project-Specific Rules

#### JsonSerializer

Always use source-generated context (`CineJsonContext`) for serialization:

```csharp
// ❌ Wrong: reflection-based
JsonSerializer.Deserialize<PipState>(json);

// ✅ Correct: source-generated
JsonSerializer.Deserialize(json, CineJsonContext.Default.PipState);
```

#### Error Handling

- **User operations**: catch + log warning + return null/fallback
- **Background ops**: catch + log error, do not rethrow
- **Dispose**: catch + log error, never throw
- **Never use bare `catch { }`** — always catch `Exception ex` and log

#### Injecting Dependencies

```csharp
// Services in MainViewModel constructor - optional with defaults
public MainViewModel(
    IMediaPlayer player,
    ISessionService? session = null,
    IPlaylistService? playlistCoordinator = null,
    IRendererService? rendererService = null,
    IMediaFileService? mediaFileService = null,
    IFileDialogService? fileDialogService = null)
```

### 8. Review Criteria

PRs are evaluated on:

| Criterion | Acceptable | Needs Work |
|---|---|---|
| Tests pass | ✅ All green | ❌ Any failure |
| No nullable warnings | ✅ 0 warnings | ❌ Any warning |
| Follows service pattern | ✅ Interface + impl | ❌ Concrete only |
| Error handling | ✅ Logged | ❌ Silent catch |
| XML docs | ✅ Public API documented | ❌ Missing summary |
| Comments | ✅ Explains "why" | ❌ Explains "what" |

## Getting Help

- Open a GitHub Discussion for questions
- File an issue for bugs or feature requests
- Tag maintainers for urgent matters
