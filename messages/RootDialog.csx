using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

[Serializable]
public class RootDialog : IDialog<object>
{
    private const string elementaryOption = "초등";
    private const string middleOption = "중";
    private const string highOption = "고등";

    public async Task StartAsync(IDialogContext context)
    {
        context.Wait(this.MessageReceivedAsync);
    }

    public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
    {
        PromptDialog.Choice(context, this.OnOptionSelected, new List<string>() { elementaryOption, middleOption, highOption }, "우리 친구는 몇학년이니?", "Not a valid option", 3);
    }


    private async Task OnOptionSelected(IDialogContext context, IAwaitable<object> result)
    {
        var message = await argument as Activity;

        try
        {
            string optionSelected = await result;



            switch (optionSelected)
            {
                case FlightsOption:
                    context.Call(new FlightsDialog(), this.ResumeAfterOptionDialog);
                    break;

                case HotelsOption:
                    context.Call(new HotelsDialog(), this.ResumeAfterOptionDialog);
                    break;
            }
        }
        catch (TooManyAttemptsException ex)
        {
            await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

            context.Wait(this.MessageReceivedAsync);
        }
    }

    private async Task ResumeAfterSupportDialog(IDialogContext context, IAwaitable<int> result)
    {
        var ticketNumber = await result;

        await context.PostAsync($"Thanks for contacting our support team. Your ticket number is {ticketNumber}.");
        context.Wait(this.MessageReceivedAsync);
    }

    private async Task ResumeAfterOptionDialog(IDialogContext context, IAwaitable<object> result)
    {
        try
        {
            var message = await result;
        }
        catch (Exception ex)
        {
            await context.PostAsync($"Failed with message: {ex.Message}");
        }
        finally
        {
            context.Wait(this.MessageReceivedAsync);
        }
    }
}