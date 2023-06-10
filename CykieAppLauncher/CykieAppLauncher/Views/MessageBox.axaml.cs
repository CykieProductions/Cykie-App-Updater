using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CykieAppLauncher;
using CykieAppLauncher.Views;

namespace MsgBox;

public enum MessageBoxButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel
}

/*public enum MessageBoxResult
{
    Ok,
    Cancel,
    Yes,
    No
}*/
public enum MessageBoxResult
{
    Accept,
    Decline,
    Cancel
}

//https://stackoverflow.com/questions/55706291/how-to-show-a-message-box-in-avaloniaui-beta
partial class MessageBox : Window
{

    public MessageBox()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static async Task<MessageBoxResult> Show(Window? parent, string title, string message, MessageBoxButtons buttons)
    {
        var view = MainView.Current;
        if (view == null) return MessageBoxResult.Cancel;

        if (false)
        {
            var msgbox = new MessageBox()
            {
                Title = title
            };
            msgbox.FindControl<TextBlock>("Text").Text = message;
            var buttonPanel = msgbox.FindControl<StackPanel>("Buttons");

            var res = MessageBoxResult.Accept;

            void AddButton(string caption, MessageBoxResult r, bool def = false)
            {
                var btn = new Button { Content = caption };
                btn.Click += (_, __) =>
                {
                    res = r;
                    msgbox.Close();
                };
                buttonPanel.Children.Add(btn);
                if (def)
                    res = r;
            }

            if (buttons == MessageBoxButtons.Ok || buttons == MessageBoxButtons.OkCancel)
                AddButton("Ok", MessageBoxResult.Accept, true);
            if (buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel)
            {
                AddButton("Yes", MessageBoxResult.Accept);
                AddButton("No", MessageBoxResult.Decline, true);
            }

            if (buttons == MessageBoxButtons.OkCancel || buttons == MessageBoxButtons.YesNoCancel)
                AddButton("Cancel", MessageBoxResult.Cancel, true);


            var tcs = new TaskCompletionSource<MessageBoxResult>();
            msgbox.Closed += delegate { res = MessageBoxResult.Cancel; tcs.TrySetResult(res); };
            if (parent != null)
                await msgbox.ShowDialog(parent);
            else msgbox.Show();
            return tcs.Task.Result;
        }
        else
        {
            var buttonPanel = view.MBoxButtons;//view.FindControl<StackPanel>("Buttons");
            buttonPanel.Children.Clear();
            var mb = view.MessageBox;

            mb.IsVisible = true;
            mb.IsEnabled = true;

            view.MBoxTitle.Text = title;
            view.MBoxMessage.Text = message;

            MessageBoxResult? result = null;

            Task<MessageBoxResult> answer = Task.Run(() =>
            {
                //Don't move on until a choice is made
                while (result == null) ;
                return result.Value;
            });

            void AddButton(string caption, MessageBoxResult r)
            {
                var btn = new Button { Content = caption };
                btn.Click += (_, __) =>
                {
                    mb.IsVisible = false;
                    mb.IsEnabled = false;
                    result = r;
                };
                buttonPanel.Children.Add(btn);
            }

            if (buttons == MessageBoxButtons.Ok || buttons == MessageBoxButtons.OkCancel)
                AddButton("Ok", MessageBoxResult.Accept);
            if (buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel)
            {
                AddButton("Yes", MessageBoxResult.Accept);
                AddButton("No", MessageBoxResult.Decline);
            }

            if (buttons == MessageBoxButtons.OkCancel || buttons == MessageBoxButtons.YesNoCancel)
                AddButton("Cancel", MessageBoxResult.Cancel);

            return await answer;
        }
    }


}