using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;

namespace DBFAUpdater;

public class StateMachine
{
    public string? Previous { get; set; }
    public required string Current { get; set; }
    public string? Next { get; set; }

    public Func<FormModel, bool>? Condition { get; set; }
}
public partial class MainWindow : Window
{
    private readonly List<StateMachine> states = new List<StateMachine>
    {
        new StateMachine { Previous = null, Current = "Welcome", Next = "Version", Condition = null },
        new StateMachine { Previous = "Welcome", Current = "Version", Next = "Profile", Condition = null },
        new StateMachine { Previous = "Version", Current = "Profile", Next = "Edition", Condition = (context) => context.Version == VersionEnum.Beta },
        new StateMachine { Previous = "Profile", Current = "Edition", Next = "Addon", Condition = null },
        new StateMachine { Previous = "Edition", Current = "Addon", Next = "Path", Condition = null },
        new StateMachine { Previous = "Addon", Current = "Path", Next = "Progress", Condition = null },
        new StateMachine { Previous = null, Current = "Progress", Next = "End", Condition = null },
        new StateMachine { Previous = null, Current = "End", Next = null, Condition = null },

    };
    private StateMachine currentState;
    public MainWindow()
    {
        InitializeComponent();
        currentState = states[0];

        
    }

    private void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((FormModel)DataContext).PropertyChanged += OnEditionChanged;
    }

    private void Next_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleState();
    }

    private void Back_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleState(false);
    }

    private void HandleState(bool direction = true)
    {
        FormModel dataForm = (FormModel)this.DataContext;
        Control? currentFrame = null;
        Control? upcomingFrame = null;
        StateMachine? upcomingState = direction ? states.GetNextState(currentState) : states.GetPreviousState(currentState);
        if (upcomingState == null)
        {
            this.Close();
            return;
        }

        int found = 0;
        foreach (var child in Main.Children)
        {
            if (child.Name == currentState.Current)
            {
                currentFrame = child;
                found++;
            }

            if (child.Name == upcomingState.Current)
            {
                upcomingFrame = child;
                found++;
            }
            if (found == 2)
            {
                break;
            }
        }

        currentFrame.IsVisible = false;
        currentFrame.IsEnabled = false;
        if (upcomingState.Condition != null && !upcomingState.Condition(dataForm))
        {
            currentState = upcomingState;
            HandleState(direction);
            return;
        }
        upcomingFrame.IsVisible = true;
        upcomingFrame.IsEnabled = true;


        Back.IsVisible = upcomingState.Previous != null;
        Back.IsEnabled = upcomingState.Previous != null;
        Next.IsVisible = upcomingState.Current != "Progress";
        Next.IsEnabled = upcomingState.Previous != "Progress";

        currentState = upcomingState;

    }

    private void OnEditionChanged(object sender, PropertyChangedEventArgs e)
    {
        FormModel dataModel = ((FormModel)this.DataContext);
        if (e.PropertyName == "Edition")
        {
            Addon2.IsEnabled = dataModel.Edition == EditionEnum.Classic;
            Addon2.IsVisible = dataModel.Edition == EditionEnum.Classic;
            ClassicPathTitle.IsEnabled = dataModel.Edition == EditionEnum.Classic;
            ClassicPathTitle.IsVisible = dataModel.Edition == EditionEnum.Classic;
            ClassicPath.IsEnabled = dataModel.Edition == EditionEnum.Classic;
            ClassicPath.IsVisible = dataModel.Edition == EditionEnum.Classic;

        }
    }

    
}