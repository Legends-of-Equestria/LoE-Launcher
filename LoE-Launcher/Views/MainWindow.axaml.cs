using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using LoE_Launcher.ViewModels;
using NLog;

namespace LoE_Launcher.Views;

public partial class MainWindow : Window
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private Image _logoImage = null!;
    private Rectangle _titleBarArea = null!;
    private Rectangle _optionsTitleBarArea = null!;
    private Rectangle _optionsScrim = null!;

    private bool _isDraggingLogo;
    private Point _lastLogoPosition;
    private Point _pointerStartPosition;
    private Point _lastPointerPosition;
    private readonly TransformGroup _logoTransform = new();
    private ScaleTransform _logoScaleTransform = null!;
    private RotateTransform _logoRotateTransform = null!;
    private TranslateTransform _logoTranslateTransform = null!;
    private Point _logoVelocity;
    private double _logoAngularVelocity;
    private const double PhysicsReferenceFrame = 0.016;
    private bool _physicsRunning;
    private int _physicsGeneration;
    private TimeSpan? _lastPhysicsFrame;
    private double? _settleElapsed;
    private double _settleStartAngle;
    private double _settleStartScale;
    private DateTime _lastMoveTime;

    public MainWindow()
    {
        InitializeComponent();
        SetupDraggableLogo();
        SetupTitleBarDrag();
        SetupOptionsOverlay();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _logoImage = this.FindControl<Image>("logoImage")!;
        _titleBarArea = this.FindControl<Rectangle>("titleBarArea")!;
        _optionsTitleBarArea = this.FindControl<Rectangle>("optionsTitleBarArea")!;
        _optionsScrim = this.FindControl<Rectangle>("optionsScrim")!;
    }
    
    #region UI Interaction Logic
    
    private void SetupDraggableLogo()
    {
        _logoScaleTransform = new ScaleTransform();
        _logoRotateTransform = new RotateTransform();
        _logoTranslateTransform = new TranslateTransform();

        _logoTransform.Children.Add(_logoScaleTransform);
        _logoTransform.Children.Add(_logoRotateTransform);
        _logoTransform.Children.Add(_logoTranslateTransform);

        _logoImage.RenderTransform = _logoTransform;
        _logoImage.Cursor = new Cursor(StandardCursorType.Hand);
        
        _logoImage.PointerEntered += OnLogoPointerEntered;
        _logoImage.PointerPressed += OnLogoPointerPressed;
        _logoImage.PointerMoved += OnLogoPointerMoved;
        _logoImage.PointerReleased += OnLogoPointerReleased;
        _logoImage.PointerCaptureLost += OnLogoPointerCaptureLost;
    }

    private void SetupTitleBarDrag()
    {
        _titleBarArea.PointerPressed += OnTitleBarPointerPressed;
        _optionsTitleBarArea.PointerPressed += OnTitleBarPointerPressed;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void SetupOptionsOverlay()
    {
        _optionsScrim.PointerPressed += OnOptionsScrimPointerPressed;
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnOptionsScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { IsOptionsOpen: true } vm)
        {
            vm.CloseOptionsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel { IsOptionsOpen: true } vm)
        {
            vm.CloseOptionsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnLogoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_logoImage).Properties.IsLeftButtonPressed)
        {
            _isDraggingLogo = true;
            ToolTip.SetTip(_logoImage, null);

            _pointerStartPosition = e.GetPosition(this);
            _lastPointerPosition = _pointerStartPosition;
            _lastLogoPosition = new Point(_logoTranslateTransform.X, _logoTranslateTransform.Y);
            _lastMoveTime = DateTime.UtcNow;

            StopPhysics();
            _logoVelocity = new Point(0, 0);
            _logoAngularVelocity = 0;

            _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = 0.9;
            _logoRotateTransform.Angle = (Random.Shared.NextDouble() - 0.5) * 6;

            e.Pointer.Capture(_logoImage);
            e.Handled = true;
        }
    }

    private void OnLogoPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingLogo) return;

        var currentPosition = e.GetPosition(this);
        var currentTime = DateTime.UtcNow;
        var deltaTime = (currentTime - _lastMoveTime).TotalSeconds;

        if (deltaTime > 0)
        {
            var velocityX = (currentPosition.X - _lastPointerPosition.X) / deltaTime;
            var velocityY = (currentPosition.Y - _lastPointerPosition.Y) / deltaTime;
            _logoVelocity = new Point(_logoVelocity.X * 0.7 + velocityX * 0.3, _logoVelocity.Y * 0.7 + velocityY * 0.3);
        }

        var dragDeltaX = currentPosition.X - _pointerStartPosition.X;
        var dragDeltaY = currentPosition.Y - _pointerStartPosition.Y;

        var newX = _lastLogoPosition.X + dragDeltaX;
        var newY = _lastLogoPosition.Y + dragDeltaY;

        var constrainedPosition = ApplyBoundaryConstraints(new Point(newX, newY));
        _logoTranslateTransform.X = constrainedPosition.X;
        _logoTranslateTransform.Y = constrainedPosition.Y;

        _lastPointerPosition = currentPosition;
        _lastMoveTime = currentTime;

        e.Handled = true;
    }

    private void OnLogoPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingLogo) return;
        _logoAngularVelocity = _logoRotateTransform.Angle * 0.2;
        FinalizeLogoDrag();
        e.Handled = true;
    }

    private void OnLogoPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDraggingLogo) FinalizeLogoDrag();
    }

    private void FinalizeLogoDrag()
    {
        _isDraggingLogo = false;
        StartPhysics();
    }

    private void StartPhysics()
    {
        var generation = ++_physicsGeneration;
        _physicsRunning = true;
        _lastPhysicsFrame = null;
        _settleElapsed = null;
        RequestAnimationFrame(time => OnPhysicsFrame(time, generation));
    }

    private void StopPhysics()
    {
        _physicsGeneration++;
        _physicsRunning = false;
        _settleElapsed = null;
    }

    private void OnPhysicsFrame(TimeSpan time, int generation)
    {
        if (generation != _physicsGeneration) return;

        var deltaTime = _lastPhysicsFrame is { } last ? Math.Clamp((time - last).TotalSeconds, 0, 0.05) : 1.0 / 60;
        _lastPhysicsFrame = time;

        var keepRunning = _settleElapsed.HasValue ? StepSettle(deltaTime) : StepPhysics(deltaTime);
        if (keepRunning)
            RequestAnimationFrame(next => OnPhysicsFrame(next, generation));
        else
            _physicsRunning = false;
    }

    private bool StepPhysics(double deltaTime)
    {
        const double minVelocity = 5.0;
        const double minAngularVelocity = 0.5;

        var frames = deltaTime / PhysicsReferenceFrame;
        var friction = Math.Pow(0.95, frames);
        _logoVelocity = new Point(_logoVelocity.X * friction, _logoVelocity.Y * friction);
        _logoAngularVelocity *= Math.Pow(0.92, frames);

        var newX = _logoTranslateTransform.X + _logoVelocity.X * deltaTime;
        var newY = _logoTranslateTransform.Y + _logoVelocity.Y * deltaTime;

        var constrainedPosition = ApplyBoundaryConstraints(new Point(newX, newY));

        if (Math.Abs(constrainedPosition.X - newX) > 0.1) _logoVelocity = new Point(_logoVelocity.X * -0.4, _logoVelocity.Y * 0.8);
        if (Math.Abs(constrainedPosition.Y - newY) > 0.1) _logoVelocity = new Point(_logoVelocity.X * 0.8, _logoVelocity.Y * -0.4);

        _logoTranslateTransform.X = constrainedPosition.X;
        _logoTranslateTransform.Y = constrainedPosition.Y;

        _logoRotateTransform.Angle += _logoAngularVelocity * deltaTime;

        var currentScale = _logoScaleTransform.ScaleX;
        _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = currentScale + (1.0 - currentScale) * (1 - Math.Pow(0.9, frames));

        if (Math.Sqrt(_logoVelocity.X * _logoVelocity.X + _logoVelocity.Y * _logoVelocity.Y) < minVelocity && Math.Abs(_logoAngularVelocity) < minAngularVelocity)
        {
            _settleElapsed = 0;
            _settleStartAngle = _logoRotateTransform.Angle;
            _settleStartScale = _logoScaleTransform.ScaleX;
        }

        return true;
    }

    private bool StepSettle(double deltaTime)
    {
        var elapsed = (_settleElapsed ?? 0) + deltaTime;
        _settleElapsed = elapsed;

        var progress = Math.Min(elapsed / 0.5, 1.0);
        var ease = 1 - Math.Pow(1 - progress, 3);
        _logoRotateTransform.Angle = _settleStartAngle * (1 - ease);
        _logoScaleTransform.ScaleX = _logoScaleTransform.ScaleY = _settleStartScale + (1.0 - _settleStartScale) * ease;

        return progress < 1.0;
    }

    private void OnLogoPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingLogo && !_physicsRunning)
        {
            ToolTip.SetTip(_logoImage, "You discovered me! Drag me around!");
        }
    }

    private Point ApplyBoundaryConstraints(Point position)
    {
        var windowWidth = Math.Max(ClientSize.Width, 800);
        var windowHeight = Math.Max(ClientSize.Height, 500);
        var logoWidth = Math.Max(_logoImage.Bounds.Width, 203);
        var logoHeight = Math.Max(_logoImage.Bounds.Height, 108);
        
        var logoInitialX = windowWidth - 20 - logoWidth;
        var logoInitialY = 40;
        
        var currentAbsoluteX = logoInitialX + position.X;
        var currentAbsoluteY = logoInitialY + position.Y;
        
        var minX = -logoWidth * 0.7;
        var maxX = windowWidth - logoWidth * 0.3;
        var minY = -logoHeight * 0.7;
        var maxY = windowHeight - logoHeight * 0.3;
        
        var constrainedX = Math.Clamp(currentAbsoluteX, minX, maxX);
        var constrainedY = Math.Clamp(currentAbsoluteY, minY, maxY);
        
        return new Point(constrainedX - logoInitialX, constrainedY - logoInitialY);
    }
    
    #endregion
}
