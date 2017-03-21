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
            var qnaword = Regex.Replace(word.Entity, @"\s+", "");
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
            if (stuff.score == "0")
            {
                findMore(qnaword, message, context);
            }
            else
            {
                await context.PostAsync($"{qnaword}의 뜻은 이거에요 {stuff.answer}");
                findMore(qnaword, message, context);
            }
            Activity replyToConversation = message.CreateReply("더 알아보고 싶나요?");

            ReceiptCard receiptCard = new ReceiptCard()
            {
                Buttons = new List<CardAction> {
                        new CardAction()
                        {
                            Title = "인터넷 검색하기"
                        },                        
                        new CardAction()
                        {
                            Title = "아뇨 계속 질문 할래요"
                        }
                    }
            };
            replyToConversation.Attachments = new List<Attachment> {
                    receiptCard.ToAttachment()
                };

            await context.PostAsync(replyToConversation);
        }
        else
        {
            await context.PostAsync($"미안해요 다시한번 말해주세요!"); //
        }
        context.Wait(MessageReceived);
    }
    [LuisIntent("greeting")]
    public async Task greeting(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"안녕~ 난 천재학습백과 상담 로봇 \"천재봇\"이야 뭐가 궁금하니?"); //
        context.Wait(MessageReceived);
    }

    public async void findMore(string qnaword, Activity message, IDialogContext context)
    {
        string strEncode = HttpUtility.UrlEncode(qnaword);
        strEncode = strEncode.ToUpper();
        string queryUri = "http://koc.chunjae.co.kr/search.do?query=" + strEncode + "&cardType=&userSearch=1&SchoolIdx=";

        // Create a new 'HttpWebRequest' object to the mentioned URL.
        HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(queryUri);
        myHttpWebRequest.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.2.13) Gecko/20101203 Firefox/3.6.13";
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

            string data = readStream.ReadToEnd();

            //파싱

            Activity replyToConversation = message.CreateReply($"천재백과에서 {qnaword}에 대해 찾아봤어요");
            replyToConversation.Recipient = message.From;
            replyToConversation.Type = "message";
            Dictionary<string, string[]> cardContentList = new Dictionary<string, string[]>();

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
                string[] tmp = new string[3];
                tmp[0] = $"http://koc.chunjae.co.kr{s_imgsrc}";
                tmp[1] = s_text;
                tmp[2] = $"http://koc.chunjae.co.kr{s_link}";
                cardContentList.Add($"{i}:{s_subject}", tmp);
                i++;
            }

            myHttpWebResponse.Close();
            readStream.Close();



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
                    Title = "WikiPedia 페이지로.."
                };
                cardButtons.Add(plButton);
               
                plButton = new CardAction()
                {                    
                    Title = "퀴즈 풀어보기"
                };
                cardButtons.Add(plButton);
                plButton = new CardAction()
                {
                    Value = $"{cardContent.Value[2]}",
                    Type = "openUrl",
                    Title = "천재학습백과 페이지로.."
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

            await context.PostAsync(replyToConversation);
        }
    }
}
