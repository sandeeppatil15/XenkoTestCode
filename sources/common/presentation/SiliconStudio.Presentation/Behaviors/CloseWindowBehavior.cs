// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace SiliconStudio.Presentation.Behaviors
{
    /// <summary>
    /// A base behavior that will close the window it is contained in an event occurs on a control. A command can be executed
    /// before closing the window by using the <see cref="Command"/> and <see cref="CommandParameter"/> properties of this behavior.
    /// </summary>
    public abstract class CloseWindowBehavior<T> : Behavior<T> where T : DependencyObject
    {
        /// <summary>
        /// Identifies the <see cref="DialogResult"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DialogResultProperty = DependencyProperty.Register("DialogResult", typeof(bool?), typeof(CloseWindowBehavior<T>));

        /// <summary>
        /// Identifies the <see cref="Command"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(CloseWindowBehavior<T>), new PropertyMetadata(null, CommandChanged));

        /// <summary>
        /// Identifies the <see cref="CommandParameter"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register("CommandParameter", typeof(object), typeof(CloseWindowBehavior<T>), new PropertyMetadata(null, CommandParameterChanged));

        /// <summary>
        /// Gets or sets the value to set to the <see cref="Window.DialogResult"/> property of the window the associated button is contained in.
        /// </summary>
        public bool? DialogResult { get { return (bool?)GetValue(DialogResultProperty); } set { SetValue(DialogResultProperty, value); } }

        /// <summary>
        /// Gets or sets the command to execute before closing the window.
        /// </summary>
        public ICommand Command { get { return (ICommand)GetValue(CommandProperty); } set { SetValue(CommandProperty, value); } }

        /// <summary>
        /// Gets or sets the parameter of the command to execute before closing the window.
        /// </summary>
        public object CommandParameter { get { return GetValue(CommandParameterProperty); } set { SetValue(CommandParameterProperty, value); } }

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            if (Command != null)
            {
                AssociatedObject.SetCurrentValue(UIElement.IsEnabledProperty, Command.CanExecute(CommandParameter));
            }
        }

        private static void CommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (ButtonCloseWindowBehavior)d;
            var oldCommand = e.OldValue as ICommand;
            var newCommand = e.NewValue as ICommand;

            if (oldCommand != null)
            {
                oldCommand.CanExecuteChanged -= behavior.CommandCanExecuteChanged;
            }
            if (newCommand != null)
            {
                newCommand.CanExecuteChanged += behavior.CommandCanExecuteChanged;
            }
        }

        private static void CommandParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (ButtonCloseWindowBehavior)d;
            if (behavior.Command != null)
            {
                behavior.AssociatedObject.SetCurrentValue(UIElement.IsEnabledProperty, behavior.Command.CanExecute(behavior.CommandParameter));
            }
        }

        private void CommandCanExecuteChanged(object sender, EventArgs e)
        {
            AssociatedObject.SetCurrentValue(UIElement.IsEnabledProperty, Command.CanExecute(CommandParameter));
        }

        /// <summary>
        /// Invokes the command and close the containing window.
        /// </summary>
        protected void Close()
        {
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }

            var window = Window.GetWindow(AssociatedObject);
            if (window == null) throw new InvalidOperationException("The button attached to this behavior is not in a window");

            bool dialogResultUpdated = false;
            // Window.DialogResult setter will throw an exception when the window was not displayed with ShowDialog, even if we're setting null.
            if (IsModal(window))
            {
                if (DialogResult != window.DialogResult)
                {
                    // Setting DialogResult to a non-null value will close the window, we don't want to invoke Close after that.
                    window.DialogResult = DialogResult;
                    dialogResultUpdated = true;
                }
            }
            else if (DialogResult != null)
            {
                throw new InvalidOperationException("The DialogResult can be set by a CloseWindowBehavior only if the window is modal");
            }

            if (DialogResult == null || !dialogResultUpdated)
            {
                window.Close();
            }
        }

        private static bool IsModal(Window window)
        {
            return (bool)typeof(Window).GetField("_showingAsDialog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        }
    }
}