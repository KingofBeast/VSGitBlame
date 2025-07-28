using Microsoft.VisualStudio.PlatformUI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSGitBlame.Core;
using Microsoft.VisualStudio.Text.Editor;
using Color = System.Drawing.Color;
using System.Collections.Generic;
using System.Windows.Input;
using System;

namespace VSGitBlame;

public class CommitInfoView
{
    public Border _container;
    public TextBlock _summaryView;
    public TextBlock _commitDetailsView;
    public StackPanel _detailsView;
    public Border _detailsViewContainer;

    public bool _firstMouseMoveFired;
    public bool _isDetailsVisible;
    public bool _showDetails;

    public bool _isMouseDown;

    public string _commitHash;

    public IAdornmentLayer _adornmentLayer;
}

public static class CommitInfoViewFactory
{
    private static (string, long)? _copiedCommit;

    private static VSGitBlamePackage _package;
    private static CommitInfoViewOptions _options;

    private static Dictionary<IAdornmentLayer, CommitInfoView> _commitInfoViews = new();

    public static void InitializeSettings(VSGitBlamePackage package)
    {
        _package = package;
        LoadSettings();
    }

    private static void LoadSettings()
    {
        if (_package != null)
        {
            _options = _package.GetDialogPage(typeof(CommitInfoViewOptions)) as CommitInfoViewOptions;
        }
    }

    public static void RefreshSettings()
    {
        LoadSettings();
        if (_options != null)
        {
            ApplySettings();
        }
    }

    private static void ApplySettings()
    {
        foreach (CommitInfoView view in _commitInfoViews.Values)
        {
            if (view._summaryView != null && _options != null)
            {
                // Apply summary view settings
                view._summaryView.FontSize = _options.SummaryFontSize;

                // Override the foreground color if specified, otherwise use theme detection
                if (_options.SummaryFontColor != Color.Transparent)
                {
                    view._summaryView.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                        _options.SummaryFontColor.A,
                        _options.SummaryFontColor.R,
                        _options.SummaryFontColor.G,
                        _options.SummaryFontColor.B));
                }
                else
                {
                    // Use theme detection if no specific color is set
                    var backgroundColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                    view._summaryView.Foreground = backgroundColor.GetBrightness() > 0.5 ?
                        Brushes.DarkBlue :
                        Brushes.LightGray;
                }
            }

            if (view._commitDetailsView != null && _options != null)
            {
                // Apply details view settings
                view._commitDetailsView.FontSize = _options.DetailsFontSize;
                view._commitDetailsView.Foreground = _options.GetDetailsFontBrush();
            }

            if (view._detailsView != null && _options != null)
            {
                // Apply background color setting
                view._detailsViewContainer.Background = _options.GetDetailsBackgroundBrush();
            }
        }
    }

    static CommitInfoViewFactory()
    {
    }

    public static Border Get(CommitInfo commitInfo, IAdornmentLayer adornmentLayer)
    {
        if (!_commitInfoViews.TryGetValue(adornmentLayer, out CommitInfoView view))
        {
            view = new();
            view._adornmentLayer = adornmentLayer;

            #region Summary View
            var backgroundColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            view._summaryView = new TextBlock
            {
                Opacity = 0.5,
                Background = Brushes.Transparent,
                Foreground = backgroundColor.GetBrightness() > 0.5 ? Brushes.DarkBlue : Brushes.LightGray,
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.Normal,
            };
            #endregion

            #region Details View
            view._detailsView = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };

            view._commitDetailsView = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
            };
            view._detailsView.Children.Add(view._commitDetailsView);

            view._detailsViewContainer = new Border
            {
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Colors.DarkBlue),
                BorderBrush = Brushes.Transparent,
                Visibility = Visibility.Hidden,
                Padding = new Thickness(5),
            };
            view._detailsViewContainer.Child = view._detailsView;
            #endregion

            #region Container
            var rootPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = Brushes.Transparent,
            };
            rootPanel.Children.Add(view._summaryView);
            rootPanel.Children.Add(view._detailsViewContainer);

            rootPanel.MouseMove += (sender, e) =>
            {
                if (!view._firstMouseMoveFired)
                {
                    view._firstMouseMoveFired = true;
                    return;
                }

            if (view._isDetailsVisible || view._showDetails == false)
                return;

                view._detailsViewContainer.Visibility = Visibility.Visible;
                view._isDetailsVisible = true;
            };

            rootPanel.MouseLeave += (sender, e) =>
            {
                view._firstMouseMoveFired = false;
                view._isDetailsVisible = false;
                view._detailsViewContainer.Visibility = Visibility.Hidden;
            };

            rootPanel.MouseLeftButtonDown += (sender, e) =>
            {
                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) >= (ModifierKeys.Control | ModifierKeys.Shift))
                    view._isMouseDown = true;
            };

            rootPanel.MouseLeftButtonUp += (sender, e) =>
            {
                if (view._isMouseDown == false)
                    return;

                view._isMouseDown = false;

                Clipboard.SetText(view._commitHash);
                view._summaryView.Text = $"Commit hash copied • {view._summaryView.Text}";

                _copiedCommit = (view._commitHash, DateTime.Now.Ticks + TimeSpan.FromSeconds(5).Ticks);
            };

            view._container = new Border
            {
                Margin = new Thickness(30, 0, 0, 0),
            };
            view._container.Child = rootPanel;
            #endregion

            _commitInfoViews[adornmentLayer] = view;
        }

        if (commitInfo.ShowDetails == false)
        {
            view._summaryView.Text = commitInfo.Summary;
            view._commitDetailsView.Text = string.Empty;
            view._detailsViewContainer.Visibility = Visibility.Hidden;
        }
        else
        {
            view._summaryView.Text = $"{commitInfo.AuthorName} • {commitInfo.Hash.Substring(0, 7)} • {commitInfo.Time:yyyy/MM/dd HH:mm} • {commitInfo.Summary}";
            view._commitDetailsView.Text =
                $"""
            {commitInfo.AuthorName} | {commitInfo.Time:f}
            {commitInfo.Summary}
            Commit: {commitInfo.Hash.Substring(0, 7)}
            """;

            if (_copiedCommit.HasValue && _copiedCommit.Value.Item1 == commitInfo.Hash && _copiedCommit.Value.Item2 > DateTime.Now.Ticks)
            {
                view._summaryView.Text = $"Commit hash copied • {view._summaryView.Text}";
            }
        }

        view._commitHash = commitInfo.Hash;

        view._showDetails = false; // commitInfo.ShowDetails;
        view._container.Visibility = Visibility.Visible;

        return view._container;
    }
}
