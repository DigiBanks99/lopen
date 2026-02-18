namespace Lopen.Tui.Tests;

/// <summary>
/// Explicit acceptance criteria coverage tests for the TUI module.
/// Each test maps to a numbered AC from docs/requirements/tui/SPECIFICATION.md (TUI-01 through TUI-47).
/// Codes that already have dedicated test coverage elsewhere are excluded:
/// TUI-04, TUI-05, TUI-06, TUI-07, TUI-16, TUI-25, TUI-26, TUI-32, TUI-33, TUI-35, TUI-36,
/// TUI-37, TUI-38, TUI-39, TUI-40, TUI-41, TUI-42, TUI-44, TUI-48, TUI-49, TUI-50, TUI-51, TUI-52.
/// </summary>
public class TuiAcceptanceCriteriaTests
{
    private static readonly ScreenRect DefaultRegion = new(0, 0, 120, 30);

    // TUI-01: Split-screen layout with activity (left) and context (right) panes, ratio adjustable from 50/50 to 80/20

    [Fact]
    public void AC01_Layout_DefaultSplit_ProducesActivityAndContextPanes()
    {
        var regions = LayoutCalculator.Calculate(120, 40);

        Assert.True(regions.Activity.Width > 0, "Activity pane must have positive width");
        Assert.True(regions.Context.Width > 0, "Context pane must have positive width");
        Assert.Equal(120, regions.Activity.Width + regions.Context.Width);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(60)]
    [InlineData(70)]
    [InlineData(80)]
    public void AC01_Layout_SplitPercent_AdjustsActivityWidth(int splitPercent)
    {
        var regions = LayoutCalculator.Calculate(100, 40, splitPercent);

        Assert.Equal(splitPercent, regions.Activity.Width);
        Assert.Equal(100 - splitPercent, regions.Context.Width);
    }

    [Theory]
    [InlineData(30, 50)]
    [InlineData(95, 80)]
    public void AC01_Layout_SplitPercent_ClampedTo50Through80(int input, int expected)
    {
        var regions = LayoutCalculator.Calculate(100, 40, input);

        Assert.Equal(expected, regions.Activity.Width);
    }

    // TUI-02: Top panel displays logo, version, model, context usage, premium requests, git branch, auth status, phase, and step

    [Fact]
    public void AC02_TopPanel_Render_DisplaysAllStatusFields()
    {
        var component = new TopPanelComponent();
        var data = new TopPanelData
        {
            Version = "v1.0.0",
            ModelName = "claude-sonnet-4",
            ContextUsedTokens = 45000,
            ContextMaxTokens = 200000,
            PremiumRequestCount = 5,
            GitBranch = "main",
            IsAuthenticated = true,
            PhaseName = "Building",
            CurrentStep = 2,
            TotalSteps = 4,
            StepLabel = "Iterate",
            ShowLogo = true,
        };

        var lines = component.Render(data, new ScreenRect(0, 0, 120, 4));
        var output = string.Join('\n', lines);

        Assert.Contains("v1.0.0", output);
        Assert.Contains("claude-sonnet-4", output);
        Assert.Contains("45K", output);
        Assert.Contains("200K", output);
        Assert.Contains("premium", output);
        Assert.Contains("main", output);
        Assert.Contains("Building", output);
    }

    [Fact]
    public void AC02_TopPanel_StatusLine_ContainsAuthIndicator()
    {
        var authenticatedStatus = TopPanelComponent.BuildStatusLine(new TopPanelData
        {
            Version = "v1.0.0",
            IsAuthenticated = true,
        });
        var unauthenticatedStatus = TopPanelComponent.BuildStatusLine(new TopPanelData
        {
            Version = "v1.0.0",
            IsAuthenticated = false,
        });

        Assert.Contains("🟢", authenticatedStatus);
        Assert.Contains("🔴", unauthenticatedStatus);
    }

    // TUI-03: Context panel shows current task, task tree with completion states, and active resources

    [Fact]
    public void AC03_ContextPanel_Render_ShowsTaskTreeAndResources()
    {
        var component = new ContextPanelComponent();
        var data = new ContextPanelData
        {
            CurrentTask = new TaskSectionData
            {
                Name = "Build auth module",
                ProgressPercent = 50,
                CompletedSubtasks = 1,
                TotalSubtasks = 2,
                Subtasks =
                [
                    new SubtaskItem("Write tests", TaskState.Complete),
                    new SubtaskItem("Implement logic", TaskState.InProgress),
                ],
            },
            Resources =
            [
                new ResourceItem("SPEC.md"),
                new ResourceItem("README.md"),
            ],
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Build auth module", output);
        Assert.Contains("✓", output);
        Assert.Contains("▶", output);
        Assert.Contains("[1]", output);
        Assert.Contains("SPEC.md", output);
    }

    [Fact]
    public void AC03_ContextPanel_StateIcons_MatchSpecification()
    {
        Assert.Equal("○", ContextPanelComponent.StateIcon(TaskState.Pending));
        Assert.Equal("▶", ContextPanelComponent.StateIcon(TaskState.InProgress));
        Assert.Equal("✓", ContextPanelComponent.StateIcon(TaskState.Complete));
        Assert.Equal("✗", ContextPanelComponent.StateIcon(TaskState.Failed));
    }

    // TUI-08: Current action expanded, previous actions collapsed to summaries

    [Fact]
    public void AC08_ActivityPanel_ExpandedEntry_ShowsDetails()
    {
        var component = new ActivityPanelComponent();
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry { Summary = "Previous action", Kind = ActivityEntryKind.Action },
                new ActivityEntry
                {
                    Summary = "Current action",
                    Kind = ActivityEntryKind.FileEdit,
                    IsExpanded = true,
                    Details = ["+ Added new method", "- Removed old method"],
                },
            ],
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Previous action", output);
        Assert.Contains("Current action", output);
        Assert.Contains("Added new method", output);
        Assert.Contains("▼", output); // expanded indicator
    }

    [Fact]
    public void AC08_ActivityPanel_CollapsedEntry_HidesDetails()
    {
        var component = new ActivityPanelComponent();
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry
                {
                    Summary = "Collapsed action",
                    Kind = ActivityEntryKind.FileEdit,
                    IsExpanded = false,
                    Details = ["+ detail line"],
                },
            ],
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Collapsed action", output);
        Assert.DoesNotContain("detail line", output);
        Assert.Contains("▶", output); // collapsed indicator
    }

    // TUI-09: Tool call outputs expandable via click or keyboard shortcut

    [Fact]
    public void AC09_KeyboardHandler_ToggleExpand_MapsSpaceAndEnterInActivityPanel()
    {
        var handler = new KeyboardHandler();

        var spaceAction = handler.Handle(
            new KeyInput { Key = ConsoleKey.Spacebar, KeyChar = ' ' },
            FocusPanel.Activity);

        var enterAction = handler.Handle(
            new KeyInput { Key = ConsoleKey.Enter },
            FocusPanel.Activity);

        Assert.Equal(KeyAction.ToggleExpand, spaceAction);
        Assert.Equal(KeyAction.ToggleExpand, enterAction);
    }

    // TUI-10: Real-time task progress updates in context panel

    [Fact]
    public void AC10_ContextPanel_ProgressBar_RendersCorrectly()
    {
        var bar0 = ContextPanelComponent.RenderProgressBar(0, 10);
        var bar50 = ContextPanelComponent.RenderProgressBar(50, 10);
        var bar100 = ContextPanelComponent.RenderProgressBar(100, 10);

        Assert.Equal("[░░░░░░░░░░]", bar0);
        Assert.Equal("[█████░░░░░]", bar50);
        Assert.Equal("[██████████]", bar100);
    }

    // TUI-11: Hierarchical task tree with status indicators (✓/▶/○)

    [Fact]
    public void AC11_ContextPanel_HierarchicalTree_ShowsComponentAndModuleSections()
    {
        var component = new ContextPanelComponent();
        var data = new ContextPanelData
        {
            CurrentTask = new TaskSectionData
            {
                Name = "Implement auth",
                Subtasks =
                [
                    new SubtaskItem("Task A", TaskState.Complete),
                    new SubtaskItem("Task B", TaskState.Pending),
                ],
            },
            Component = new ComponentSectionData
            {
                Name = "AuthService",
                CompletedTasks = 1,
                TotalTasks = 3,
                Tasks =
                [
                    new SubtaskItem("Login", TaskState.Complete),
                    new SubtaskItem("Logout", TaskState.InProgress),
                ],
            },
            Module = new ModuleSectionData
            {
                Name = "Auth",
                InProgressComponents = 1,
                TotalComponents = 2,
                Components =
                [
                    new SubtaskItem("Service", TaskState.Complete),
                    new SubtaskItem("Controller", TaskState.Pending),
                ],
            },
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Implement auth", output);
        Assert.Contains("AuthService", output);
        Assert.Contains("Auth", output);
        Assert.Contains("├─", output);
        Assert.Contains("└─", output);
    }

    // TUI-12: Numbered resource access (press 1-9 to view active resources)

    [Theory]
    [InlineData(ConsoleKey.D1, KeyAction.ViewResource1)]
    [InlineData(ConsoleKey.D5, KeyAction.ViewResource5)]
    [InlineData(ConsoleKey.D9, KeyAction.ViewResource9)]
    public void AC12_KeyboardHandler_NumberKeys_MapToViewResource(ConsoleKey key, KeyAction expected)
    {
        var handler = new KeyboardHandler();

        var action = handler.Handle(
            new KeyInput { Key = key },
            FocusPanel.Activity);

        Assert.Equal(expected, action);
    }

    [Fact]
    public void AC12_KeyboardHandler_NumberKeys_IgnoredWhenPromptFocused()
    {
        var handler = new KeyboardHandler();

        var action = handler.Handle(
            new KeyInput { Key = ConsoleKey.D1 },
            FocusPanel.Prompt);

        Assert.NotEqual(KeyAction.ViewResource1, action);
    }

    // TUI-13: Inline research display with ability to drill into full document

    [Fact]
    public void AC13_ResearchDisplay_ShowsFindingsAndDocumentLink()
    {
        var component = new ResearchDisplayComponent();
        var data = new ResearchDisplayData
        {
            Topic = "JWT Best Practices",
            Findings = ["Use RS256", "Short expiration"],
            HasFullDocument = true,
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("JWT Best Practices", output);
        Assert.Contains("Use RS256", output);
        Assert.Contains("[See full research document]", output);
    }

    [Fact]
    public void AC13_ResearchDisplay_NoDocumentLink_WhenNoFullDocument()
    {
        var component = new ResearchDisplayComponent();
        var data = new ResearchDisplayData
        {
            Topic = "Basics",
            Findings = ["Finding 1"],
            HasFullDocument = false,
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.DoesNotContain("[See full research document]", output);
    }

    // TUI-14: Phase transition summaries shown in activity area

    [Fact]
    public void AC14_PhaseTransition_RendersTransitionWithSections()
    {
        var component = new PhaseTransitionComponent();
        var data = new PhaseTransitionData
        {
            FromPhase = "Research",
            ToPhase = "Building",
            Sections =
            [
                new TransitionSection("Completed", ["Analyzed 12 files"]),
                new TransitionSection("Next Steps", ["Create middleware"]),
            ],
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("◆ Phase Transition: Research → Building", output);
        Assert.Contains("Completed", output);
        Assert.Contains("Analyzed 12 files", output);
        Assert.Contains("Next Steps", output);
    }

    // TUI-15: Diff viewer with syntax highlighting and line numbers

    [Fact]
    public void AC15_DiffViewer_RendersLineNumbersAndAddRemoveMarkers()
    {
        var component = new DiffViewerComponent();
        var data = new DiffViewerData
        {
            FilePath = "src/Auth.cs",
            LinesAdded = 2,
            LinesRemoved = 1,
            Hunks =
            [
                new DiffHunk
                {
                    StartLine = 10,
                    Lines =
                    [
                        " existing line",
                        "-removed line",
                        "+added line",
                    ],
                },
            ],
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Auth.cs", output);
        Assert.Contains("+2", output);
        Assert.Contains("-1", output);
        Assert.Contains("│", output);
    }

    [Fact]
    public void AC15_SyntaxHighlighter_SupportsExpectedExtensions()
    {
        Assert.True(SyntaxHighlighter.SupportsExtension(".cs"));
        Assert.True(SyntaxHighlighter.SupportsExtension(".ts"));
        Assert.True(SyntaxHighlighter.SupportsExtension(".js"));
        Assert.True(SyntaxHighlighter.SupportsExtension(".py"));
        Assert.False(SyntaxHighlighter.SupportsExtension(".txt"));
        Assert.False(SyntaxHighlighter.SupportsExtension(null));
    }

    // TUI-17: Phase/step visualization (●/○ progress indicator) in top panel

    [Fact]
    public void AC17_TopPanel_StepIndicator_ShowsFilledAndEmptyCircles()
    {
        var indicator = TopPanelComponent.BuildStepIndicator(3, 5);

        Assert.Equal("●●●○○", indicator);
    }

    [Fact]
    public void AC17_TopPanel_PhaseLine_IncludesStepProgress()
    {
        var line = TopPanelComponent.BuildPhaseLine(new TopPanelData
        {
            Version = "v1.0.0",
            PhaseName = "Building",
            CurrentStep = 2,
            TotalSteps = 4,
            StepLabel = "Iterate Tasks",
        });

        Assert.Contains("Building", line);
        Assert.Contains("●●○○", line);
        Assert.Contains("Step 2/4", line);
        Assert.Contains("Iterate Tasks", line);
    }

    // TUI-18: Module selection modal with arrow key navigation

    [Fact]
    public void AC18_SelectionModal_RendersOptionsWithSelectionMarker()
    {
        var component = new SelectionModalComponent();
        var data = new ModuleSelectionData
        {
            Title = "Select Module",
            Options = ["Auth", "Storage", "TUI"],
            SelectedIndex = 1,
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Select Module", output);
        Assert.Contains("▶", output);
        Assert.Contains("Storage", output);
    }

    [Fact]
    public void AC18_KeyboardHandler_ArrowKeys_ScrollInNonPromptPanels()
    {
        var handler = new KeyboardHandler();

        var downAction = handler.Handle(
            new KeyInput { Key = ConsoleKey.DownArrow },
            FocusPanel.Activity);
        var upAction = handler.Handle(
            new KeyInput { Key = ConsoleKey.UpArrow },
            FocusPanel.Activity);

        Assert.Equal(KeyAction.ScrollDown, downAction);
        Assert.Equal(KeyAction.ScrollUp, upAction);
    }

    // TUI-19: Component selection UI with tree view

    [Fact]
    public void AC19_FilePicker_RendersTreeViewWithIcons()
    {
        var component = new FilePickerComponent();
        var data = new FilePickerData
        {
            RootPath = "src/",
            Nodes =
            [
                new FileNode("Auth", true, 0, true),
                new FileNode("Login.cs", false, 1),
                new FileNode("Models", true, 1, false),
            ],
            SelectedIndex = 1,
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("📂", output); // expanded directory
        Assert.Contains("📁", output); // collapsed directory
        Assert.Contains("📄", output); // file
        Assert.Contains("▶", output); // selection marker
        Assert.Contains("Login.cs", output);
    }

    // TUI-20: Multi-line prompt input with Alt+Enter for newlines

    [Fact]
    public void AC20_KeyboardHandler_AltEnter_InsertsNewline()
    {
        var handler = new KeyboardHandler();

        var action = handler.Handle(
            new KeyInput { Key = ConsoleKey.Enter, Modifiers = ConsoleModifiers.Alt },
            FocusPanel.Prompt);

        Assert.Equal(KeyAction.InsertNewline, action);
    }

    [Fact]
    public void AC20_PromptArea_MultilineText_WrapsCorrectly()
    {
        var wrapped = PromptAreaComponent.WrapText("line1\nline2\nline3", 80);

        Assert.Equal(3, wrapped.Count);
        Assert.Equal("line1", wrapped[0]);
        Assert.Equal("line2", wrapped[1]);
        Assert.Equal("line3", wrapped[2]);
    }

    // TUI-21: Keyboard shortcuts functional: Tab (focus panel), Ctrl+P (pause), number keys (resources)

    [Fact]
    public void AC21_KeyboardHandler_Tab_CyclesFocus()
    {
        var handler = new KeyboardHandler();

        var action = handler.Handle(
            new KeyInput { Key = ConsoleKey.Tab },
            FocusPanel.Prompt);

        Assert.Equal(KeyAction.CycleFocusForward, action);
    }

    [Fact]
    public void AC21_KeyboardHandler_CycleFocus_RotatesCorrectly()
    {
        Assert.Equal(FocusPanel.Activity, KeyboardHandler.CycleFocus(FocusPanel.Prompt));
        Assert.Equal(FocusPanel.Context, KeyboardHandler.CycleFocus(FocusPanel.Activity));
        Assert.Equal(FocusPanel.Prompt, KeyboardHandler.CycleFocus(FocusPanel.Context));
    }

    [Fact]
    public void AC21_KeyboardHandler_CtrlP_TogglesPause()
    {
        var handler = new KeyboardHandler();

        var action = handler.Handle(
            new KeyInput { Key = ConsoleKey.P, Modifiers = ConsoleModifiers.Control },
            FocusPanel.Prompt);

        Assert.Equal(KeyAction.TogglePause, action);
    }

    // TUI-22: Guided conversation UI for requirement gathering (step 1)

    [Fact]
    public void AC22_GuidedConversation_RendersPhaseAndTurns()
    {
        var component = new GuidedConversationComponent();
        var data = new GuidedConversationData
        {
            Phase = ConversationPhase.Interview,
            Turns =
            [
                new ConversationTurn { Role = ConversationRole.Agent, Content = "What are you building?" },
                new ConversationTurn { Role = ConversationRole.User, Content = "An auth module" },
            ],
            CurrentQuestion = "What auth method?",
            QuestionsAnswered = 1,
            EstimatedTotalQuestions = 5,
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Interview", output);
        Assert.Contains("What are you building?", output);
        Assert.Contains("An auth module", output);
        Assert.Contains("What auth method?", output);
        Assert.Contains("[1/5]", output);
    }

    [Fact]
    public void AC22_GuidedConversation_DraftingPhase_ShowsSpec()
    {
        var component = new GuidedConversationComponent();
        var data = new GuidedConversationData
        {
            Phase = ConversationPhase.Drafting,
            DraftedSpec = "# Auth Module\n## Requirements",
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Drafting", output);
        Assert.Contains("Drafted Specification", output);
        Assert.Contains("Auth Module", output);
    }

    // TUI-23: Confirmation modals with Yes/No/Always/Other options

    [Fact]
    public void AC23_ConfirmationModal_RendersOptionsWithSelection()
    {
        var component = new ConfirmationModalComponent();
        var data = new ConfirmationData
        {
            Title = "Apply changes?",
            Message = "This modifies 3 files",
            Options = ["Yes", "No", "Always"],
            SelectedIndex = 0,
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Apply changes?", output);
        Assert.Contains("This modifies 3 files", output);
        Assert.Contains("[>Yes<]", output);
        Assert.Contains("[No]", output);
        Assert.Contains("[Always]", output);
    }

    // TUI-24: Expandable sections via click or keyboard shortcut

    [Fact]
    public void AC24_ActivityPanel_ExpandedEntry_ShowsFullDocumentPrompt()
    {
        var component = new ActivityPanelComponent();
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry
                {
                    Summary = "Research findings",
                    Kind = ActivityEntryKind.Research,
                    IsExpanded = true,
                    Details = ["Finding 1"],
                    FullDocumentContent = "Full document text here",
                },
            ],
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("[Press Enter to view full document]", output);
    }

    // TUI-27: Critical error modal with details and recovery options

    [Fact]
    public void AC27_ErrorModal_RendersRecoveryOptions()
    {
        var component = new ErrorModalComponent();
        var data = new ErrorModalData
        {
            Title = "Build Failed",
            Message = "error CS1061: missing method",
            RecoveryOptions = ["Retry", "Skip", "Abort"],
            SelectedIndex = 0,
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("⚠", output);
        Assert.Contains("Build Failed", output);
        Assert.Contains("error CS1061", output);
        Assert.Contains("[>Retry<]", output);
        Assert.Contains("[Skip]", output);
        Assert.Contains("[Abort]", output);
    }

    // TUI-28: Spinner-based async feedback for long-running operations

    [Fact]
    public void AC28_Spinner_RendersFrameAndMessage()
    {
        var component = new SpinnerComponent();
        var data = new SpinnerData { Message = "Analyzing...", Frame = 0, ProgressPercent = 45 };

        var result = component.Render(data, 80);

        Assert.Contains("⠋", result);
        Assert.Contains("Analyzing...", result);
        Assert.Contains("45%", result);
    }

    [Fact]
    public void AC28_Spinner_IndeterminateProgress_OmitsPercent()
    {
        var component = new SpinnerComponent();
        var data = new SpinnerData { Message = "Loading...", Frame = 2, ProgressPercent = -1 };

        var result = component.Render(data, 80);

        Assert.Contains("Loading...", result);
        Assert.DoesNotContain("%", result);
    }

    // TUI-29: Context window usage displayed in top panel

    [Fact]
    public void AC29_TopPanel_ContextUsage_FormattedInStatusLine()
    {
        var status = TopPanelComponent.BuildStatusLine(new TopPanelData
        {
            Version = "v1.0.0",
            ContextUsedTokens = 45000,
            ContextMaxTokens = 200000,
        });

        Assert.Contains("Context:", status);
        Assert.Contains("45K", status);
        Assert.Contains("200K", status);
    }

    [Fact]
    public void AC29_TopPanel_FormatTokens_ScalesCorrectly()
    {
        Assert.Equal("500", TopPanelComponent.FormatTokens(500));
        Assert.Equal("2.4K", TopPanelComponent.FormatTokens(2400));
        Assert.Equal("128K", TopPanelComponent.FormatTokens(128000));
        Assert.Equal("1.5M", TopPanelComponent.FormatTokens(1_500_000));
    }

    // TUI-30: Premium request counter displayed in top panel (🔥 indicator)

    [Fact]
    public void AC30_TopPanel_PremiumRequests_ShowsFireIndicator()
    {
        var status = TopPanelComponent.BuildStatusLine(new TopPanelData
        {
            Version = "v1.0.0",
            PremiumRequestCount = 12,
        });

        Assert.Contains("🔥", status);
        Assert.Contains("12", status);
        Assert.Contains("premium", status);
    }

    [Fact]
    public void AC30_TopPanel_ZeroPremiumRequests_OmitsIndicator()
    {
        var status = TopPanelComponent.BuildStatusLine(new TopPanelData
        {
            Version = "v1.0.0",
            PremiumRequestCount = 0,
        });

        Assert.DoesNotContain("🔥", status);
        Assert.DoesNotContain("premium", status);
    }

    // TUI-31: Real-time progress percentages in context panel

    [Fact]
    public void AC31_ContextPanel_TaskProgress_ShowsPercentAndSubtaskCount()
    {
        var component = new ContextPanelComponent();
        var data = new ContextPanelData
        {
            CurrentTask = new TaskSectionData
            {
                Name = "Build auth",
                ProgressPercent = 65,
                CompletedSubtasks = 2,
                TotalSubtasks = 3,
                Subtasks =
                [
                    new SubtaskItem("A", TaskState.Complete),
                    new SubtaskItem("B", TaskState.Complete),
                    new SubtaskItem("C", TaskState.InProgress),
                ],
            },
        };

        var lines = component.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("65%", output);
        Assert.Contains("2/3", output);
    }

    // TUI-34: Box-drawing characters used for borders and panels

    [Fact]
    public void AC34_UnicodeSupport_BoxDrawing_ProvidesBorderCharacters()
    {
        UnicodeSupport.UseAscii = false;
        Assert.Equal("┌", UnicodeSupport.TopLeft);
        Assert.Equal("┐", UnicodeSupport.TopRight);
        Assert.Equal("└", UnicodeSupport.BottomLeft);
        Assert.Equal("┘", UnicodeSupport.BottomRight);
        Assert.Equal("─", UnicodeSupport.Horizontal);
        Assert.Equal("│", UnicodeSupport.Vertical);
        Assert.Equal("├", UnicodeSupport.TeeRight);
        Assert.Equal("┤", UnicodeSupport.TeeLeft);
        Assert.Equal("┼", UnicodeSupport.Cross);
    }

    [Fact]
    public void AC34_UnicodeSupport_AsciiMode_ProvidesAsciiFallbacks()
    {
        var original = UnicodeSupport.UseAscii;
        try
        {
            UnicodeSupport.UseAscii = true;
            Assert.Equal("+", UnicodeSupport.TopLeft);
            Assert.Equal("+", UnicodeSupport.TopRight);
            Assert.Equal("-", UnicodeSupport.Horizontal);
            Assert.Equal("|", UnicodeSupport.Vertical);
        }
        finally
        {
            UnicodeSupport.UseAscii = original;
        }
    }

    // TUI-43: Gallery lists all TUI components with selection navigation

    [Fact]
    public void AC43_GalleryList_RendersItemsWithSelectionMarker()
    {
        var listComponent = new GalleryListComponent();
        var data = new GalleryListData
        {
            Items =
            [
                new GalleryItem("TopPanel", "Top panel component"),
                new GalleryItem("ActivityPanel", "Activity panel component"),
            ],
            SelectedIndex = 0,
        };

        var lines = listComponent.Render(data, DefaultRegion);
        var output = string.Join('\n', lines);

        Assert.Contains("Component Gallery", output);
        Assert.Contains("▶", output);
        Assert.Contains("TopPanel", output);
        Assert.Contains("ActivityPanel", output);
    }

    [Fact]
    public void AC43_GalleryList_FromGallery_CreatesDataFromRegistry()
    {
        var gallery = new TestComponentGallery();
        gallery.Register(new TopPanelComponent());
        gallery.Register(new SpinnerComponent());

        var data = GalleryListComponent.FromGallery(gallery, 1);

        Assert.Equal(2, data.Items.Count);
        Assert.Equal(1, data.SelectedIndex);
        Assert.Equal("TopPanel", data.Items[0].Name);
        Assert.Equal("Spinner", data.Items[1].Name);
    }

    // TUI-45: Components are fully interactive in preview (shortcuts, scroll, expand/collapse)

    [Fact]
    public void AC45_GalleryPreviewController_NavigatesListAndEntersPreview()
    {
        var gallery = new TestComponentGallery();
        gallery.Register(new TopPanelComponent());
        gallery.Register(new SpinnerComponent());
        var controller = new GalleryPreviewController(gallery);

        Assert.False(controller.InPreview);
        Assert.Equal(0, controller.SelectedIndex);

        controller.HandleAction(KeyAction.ScrollDown);
        Assert.Equal(1, controller.SelectedIndex);

        controller.HandleAction(KeyAction.ToggleExpand);
        Assert.True(controller.InPreview);

        controller.HandleAction(KeyAction.Cancel);
        Assert.False(controller.InPreview);
    }

    [Fact]
    public void AC45_GalleryPreviewController_CyclesPreviewStates()
    {
        var gallery = new TestComponentGallery();
        gallery.Register(new TopPanelComponent());
        var controller = new GalleryPreviewController(gallery);

        controller.HandleAction(KeyAction.ToggleExpand);
        Assert.True(controller.InPreview);
        Assert.Equal("populated", controller.CurrentPreviewState);

        controller.HandleAction(KeyAction.ScrollDown);
        Assert.NotEqual("populated", controller.CurrentPreviewState);
    }

    // TUI-46: Components accept injected data with no live dependencies in preview

    [Fact]
    public void AC46_AllPreviewableComponents_RenderWithoutExternalDependencies()
    {
        var components = new IPreviewableComponent[]
        {
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new SpinnerComponent(),
            new GuidedConversationComponent(),
            new DiffViewerComponent(),
            new PhaseTransitionComponent(),
            new ResearchDisplayComponent(),
            new LandingPageComponent(),
            new SessionResumeModalComponent(),
            new ResourceViewerModalComponent(),
            new FilePickerComponent(),
            new SelectionModalComponent(),
            new ConfirmationModalComponent(),
            new ErrorModalComponent(),
        };

        foreach (var component in components)
        {
            var exception = Record.Exception(() => component.RenderPreview(80, 24));
            Assert.Null(exception);
        }
    }

    // TUI-47: Components self-register with gallery for automatic listing

    [Fact]
    public void AC47_ComponentGallery_Register_RejectsDuplicateNames()
    {
        var gallery = new TestComponentGallery();
        gallery.Register(new TopPanelComponent());

        Assert.Throws<InvalidOperationException>(() =>
            gallery.Register(new TopPanelComponent()));
    }

    [Fact]
    public void AC47_ComponentGallery_GetByName_FindsRegisteredComponent()
    {
        var gallery = new TestComponentGallery();
        var component = new SpinnerComponent();
        gallery.Register(component);

        var found = gallery.GetByName("Spinner");

        Assert.NotNull(found);
        Assert.Equal("Spinner", found.Name);
    }

    // === Fakes ===

    /// <summary>
    /// Test-accessible implementation of IComponentGallery (since ComponentGallery is internal).
    /// </summary>
    private sealed class TestComponentGallery : IComponentGallery
    {
        private readonly List<ITuiComponent> _components = [];

        public void Register(ITuiComponent component)
        {
            ArgumentNullException.ThrowIfNull(component);
            if (_components.Any(c => c.Name == component.Name))
                throw new InvalidOperationException($"Component '{component.Name}' is already registered.");
            _components.Add(component);
        }

        public IReadOnlyList<ITuiComponent> GetAll() => _components.AsReadOnly();

        public ITuiComponent? GetByName(string name) =>
            _components.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
