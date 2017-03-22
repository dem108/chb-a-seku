#load "BasicLuisDialog.csx"

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;



/// This dialog is the main bot dialog, which will call the Form Dialog and handle the results
[Serializable]
public class AskGradeDialog : IDialog<object>
{

    private const string ElementaryOption = "초등";
    private const string MiddleOption = "중";
    private const string HighOption = "고등";
    protected string strUserGrade = "";
    
    public async Task StartAsync(IDialogContext context)
    {
        context.Wait(this.MessageReceivedAsync);
    }

    //public async Task MyHandler(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
    public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
    {
        //User 정보를 가져옴

        var message = await result;

        if (strUserGrade != "")//Luis로 넘김
        {
            await context.Forward(new BasicLuisDialog(), this.ResumeAfterSupportDialog, message, System.Threading.CancellationToken.None);
        }
        else //학년 정보가 없는 경우
        {
            await context.PostAsync($"만나서 반가워!"); 
            PromptDialog.Choice(context, this.OnOptionSelected, new List<string>() { ElementaryOption, MiddleOption, HighOption }, "학년이 어떻게 되니?", "Not a valid option", 3);
        }
    }

   
    private async Task OnOptionSelected(IDialogContext context, IAwaitable<string> result)
    {
        string optionSelected = await result;
        strUserGrade = optionSelected;
        await context.PostAsync($"{optionSelected} 학생이구나? 반가워. 뭐가궁금하니?"); //
    }
    private async Task ResumeAfterSupportDialog(IDialogContext context, IAwaitable<object> result)
    {
        var answer = await result;
        await context.PostAsync($"대답 :  {answer}.");
        context.Wait(this.MessageReceivedAsync);
    }
}