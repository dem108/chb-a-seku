#r "Newtonsoft.Json"
#r "System.Web"
using System;
using System.Web;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;

using Microsoft.Bot.Connector;

using Newtonsoft.Json;

// For more information about this template visit http://aka.ms/azurebots-csharp-luis
[Serializable]
public class BasicLuisDialog : LuisDialog<object>
{
    string qnaword;
    Dictionary<string, string[]> cardContentList;
    public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute("391b906b-13e3-4aff-a806-fa7de78950b2", "5e86b5619df548889f6c79bbcf4e53b5")))
    {
    }

    [LuisIntent("None")]
    public async Task NoneIntent(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"You have reached the none intent. You said: {result.Query}"); //
        context.Wait(MessageReceived);
    }

    // Go to https://luis.ai and create a new intent, then train/publish your luis app.
    // Finally replace "MyIntent" with the name of your newly created intent in the following handler
    [LuisIntent("askQuestion")]
    public async Task askQuestion(IDialogContext context, IAwaitable<object> argument, LuisResult result)
    {
        EntityRecommendation word;
        if (result.TryFindEntity("word", out word))
        {

            //word.Entity를 QnAmaker로 보내는 루틴
            var responseString = String.Empty;

            // Send question to API QnA bot
            var knowledgebaseId = "cd7b4bb7-9ce8-4908-991c-93860d1462c0"; // Use knowledge base id created.
            var qnamakerSubscriptionKey = "db48052cd99c4c0db8d411756eea5d02"; //Use subscription key assigned to you.

            //Build the URI
            Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v1.0");
            var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

            //Add the question as part of the body
            //var postBody = $"{{\"question\": \"{activity.Text}\"}}";
            qnaword = Regex.Replace(word.Entity, @"\s+", "");
            var postBody = $"{{\"question\": \"{qnaword}\"}}";


            //Send the POST request
            using (WebClient client = new WebClient())
            {
                //Set the encoding to UTF8
                client.Encoding = System.Text.Encoding.UTF8;

                //Add the subscription key header
                client.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
                client.Headers.Add("Content-Type", "application/json");
                responseString = client.UploadString(builder.Uri, postBody);
            }

            //Json parsing
            dynamic stuff = JsonConvert.DeserializeObject(responseString);

            //for cardlayout
            var message = await argument as Activity;

            //receive msg
            if (!(stuff.score == "0"))
            {
                await context.PostAsync($"{qnaword}의 뜻은 이거에요 {stuff.answer}");
            }
            findMore(message, context);

            PromptDialog.Choice(context, this.OnOptionSelected, new List<string>() { "처음부터", "아니요", "퀴즈 풀어보기" }, "더 궁금한게 있으시나요?","다시 입력해주세요", 3);
            
        }
        else
        {
            await context.PostAsync($"미안해요 미안 안알랴줌. 노코멘트하겠어!"); //
        }
    }
    //context.Call(new MenuDialog(), (context, result) => { /*Do something. at this point your are back to the parent dialog.*/});
    private async Task OnOptionSelected(IDialogContext context, IAwaitable<string> result)
    {
        string optionSelected = await result;
        if (optionSelected.Equals("처음부터"))
        {
            await context.PostAsync($"궁금한건 언제든지 물어보세요!"); //
        }
        else if (optionSelected.Equals("퀴즈 풀어보기"))
        { 
            await context.PostAsync($"풀어보고 싶은 문제 번호를 입력해 주세요!"); //
            context.Wait(ChoiceProblemNumber);
        }
        else
        {
            await context.PostAsync($"잘가~ 궁금한건 또 물어봐!"); //
            await context.PostAsync($"요기에 재밌는 정보들이 많이 있어! http://koc.chunjae.co.kr/main.do"); //
        }
    }

    public async Task ChoiceProblemNumber(IDialogContext context, IAwaitable<IMessageActivity> argument)
    {
        var message = await argument;
        await context.PostAsync(message.Text +"번 문제야!");
        findQuestion(message.Text, context);
        await context.PostAsync("광역시가 되기 위한 조건으로 옳지 않은 것은 어느 것입니까? 난이도: ★ ★ ★ ☆ ☆ ");
        await context.PostAsync("1.나라에서 인정해야 한다.\n\n2.지역 사람들이 찬성해야 한다.\n\n3.인구가 50만 명이 넘어야 한다.\n\n4.다른 도시에 의존하지 않아야 한다.\n\n5.도시 안에서 주민들의 생활이 이루어질 수 있어야 한다.");
        context.Wait(checkAnswer);


    }

    private async Task checkAnswer(IDialogContext context, IAwaitable<IMessageActivity> argument)
    {
        var message = await argument;
        if (message.Text.Equals("3"))
        {
            await context.PostAsync($"정답!"); //
        }
        else
        {
            await context.PostAsync($"오답! **정답은** 3번\n\n해설:우리나라의 광역시는 모두 인구가 100만 명이 넘는 큰 도시들이지만 인구가 많다고 해서 광역시가 될 수 있는 것은 아닙니다."); //
        }
        PromptDialog.Choice(context, this.OnOptionSelected2, new List<string>() { "처음부터", "아니요" }, "더 궁금한게 있으시나요?", "다시 입력해주세요", 3);
    }
    private async Task OnOptionSelected2(IDialogContext context, IAwaitable<string> result)
    {
        string optionSelected = await result;
        if (optionSelected.Equals("처음부터"))
        {
            await context.PostAsync($"궁금한건 언제든지 물어보세요!"); //
        }        
        else
        {
            await context.PostAsync($"잘가~ 궁금한건 또 물어봐!"); //
            await context.PostAsync($"요기에 재밌는 정보들이 많이 있어! http://koc.chunjae.co.kr/main.do"); //
        }
    }

    [LuisIntent("greeting")]
    public async Task greeting(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"안녕~ 난 천재학습백과 상담 로봇 \"천재봇\"이야 뭐가 궁금하니?"); //
        context.Wait(MessageReceived);
    }
    public string getHtmlText(string queryUri)
    {
        // Create a new 'HttpWebRequest' object to the mentioned URL.
        HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(queryUri);
        myHttpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; Touch; .NET4.0C; .NET4.0E; .NET CLR 2.0.50727; .NET CLR 3.0.30729; .NET CLR 3.5.30729; Tablet PC 2.0; InfoPath.3; rv:11.0) like Gecko";
        // Assign the response object of 'HttpWebRequest' to a 'HttpWebResponse' variable.
        HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();

        if (myHttpWebResponse.StatusCode == HttpStatusCode.OK)
        {
            Stream receiveStream = myHttpWebResponse.GetResponseStream();
            StreamReader readStream = null;

            if (myHttpWebResponse.CharacterSet == null)
            {
                readStream = new StreamReader(receiveStream);
            }
            else
            {
                readStream = new StreamReader(receiveStream, Encoding.GetEncoding(myHttpWebResponse.CharacterSet));
            }

            
            return readStream.ReadToEnd();

        }
        else
        {
            return "Error";
        }
    }
    public async void findQuestion(string problemNum, IDialogContext context)
    {

        int index = Convert.ToInt32(problemNum);
        string[] cardContentListValue = cardContentList.Values.ElementAt(index);
        string queryUri = cardContentListValue[2];


        //파싱
        string data = getHtmlText(queryUri);

        //HtmlDocument doc = new HtmlDocument();
        //doc.LoadHtml(data);
        //var contents = doc.DocumentNode.Descendants("div").Where(d => d.Attributes["class"].Value.Contains("sm-swiper-slide"));

        //int i = 0;
        //string url = "";
        //string wholeContent = "초기 값";
        //foreach (var content in contents)
        //{
        //    wholeContent = content.InnerHtml;
        //}





        await context.PostAsync(queryUri);
        //await context.PostAsync(data);

    }
    public async void findMore(Activity message, IDialogContext context)
    {
        string strEncode = HttpUtility.UrlEncode(qnaword);
        strEncode = strEncode.ToUpper();
        string queryUri = "http://koc.chunjae.co.kr/search.do?query=" + strEncode + "&cardType=&userSearch=1&SchoolIdx=";


        string data = getHtmlText(queryUri);

        //파싱

        Activity replyToConversation = message.CreateReply($"천재백과에서 {qnaword}에 대해 찾아봤어요");
        replyToConversation.Recipient = message.From;
        replyToConversation.Type = "message";
        cardContentList = new Dictionary<string, string[]>();

        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(data);
        doc.LoadHtml(doc.GetElementbyId("searchResult01").InnerHtml);
        var contents = doc.DocumentNode.Descendants("div").Where(d => d.Attributes["class"].Value.Contains("core"));
        int i = 0;
        foreach (var content in contents)
        {
            HtmlDocument tmpdoc = new HtmlDocument();
            tmpdoc.LoadHtml(content.InnerHtml);
            var links = tmpdoc.DocumentNode.Descendants("a").Where(d => d.Attributes["class"].Value.Contains("link"));
            var imgs = tmpdoc.DocumentNode.Descendants("img");
            var subjects = tmpdoc.DocumentNode.Descendants("span").Where(d => d.Attributes["class"].Value.Contains("subject"));
            var texts = tmpdoc.DocumentNode.Descendants("span").Where(d => d.Attributes["class"].Value.Contains("text")); ;

            string s_imgsrc = string.Empty;
            string s_subject = string.Empty;
            string s_text = string.Empty;
            string s_link = string.Empty;

            foreach (var img in imgs)
            {
                s_imgsrc = img.Attributes["src"].Value;
            }
            foreach (var link in links)
            {
                s_link = link.Attributes["href"].Value;
            }
            foreach (var subject in subjects)
            {
                s_subject = subject.InnerHtml;
            }
            foreach (var text in texts)
            {
                s_text = text.InnerHtml;
            }
            string[] cardContentArray = new string[3];
            cardContentArray[0] = $"http://koc.chunjae.co.kr{s_imgsrc}";
            cardContentArray[1] = s_text;
            cardContentArray[2] = $"http://koc.chunjae.co.kr{s_link}";
            cardContentList.Add($"{i}:{s_subject}", cardContentArray);
            i++;
        }

        if(i == 0)
        {
            replyToConversation = message.CreateReply($"{qnaword} 단어는 어려워... 미안 안알랴줌. 노코멘트하겠어");
        } else
        {
            foreach (KeyValuePair<string, string[]> cardContent in cardContentList)
            {
                List<CardImage> cardImages = new List<CardImage>();
                cardImages.Add(new CardImage(url: cardContent.Value[0]));

                List<CardAction> cardButtons = new List<CardAction>();

                string tmplink = cardContent.Key;
                string[] split_string = tmplink.Split(':');

                CardAction plButton = new CardAction()
                {
                    Value = $"https://ko.wikipedia.org/wiki/{split_string[1]}",
                    Type = "openUrl",
                    Title = "위키피디아에서 더보기.."
                };
                cardButtons.Add(plButton);

                plButton = new CardAction()
                {
                    Value = $"{cardContent.Value[2]}",
                    Type = "openUrl",
                    Title = "천재학습백과에서 더보기.."
                };
                cardButtons.Add(plButton);

                HeroCard plCard = new HeroCard()
                {
                    Title = $"{cardContent.Key}",
                    Subtitle = $"{cardContent.Value[1]}",
                    Images = cardImages,
                    Buttons = cardButtons
                };

                Attachment plAttachment = plCard.ToAttachment();
                replyToConversation.Attachments.Add(plAttachment);
            }

            replyToConversation.AttachmentLayout = AttachmentLayoutTypes.Carousel;

        }

        await context.PostAsync(replyToConversation);

    }
}
