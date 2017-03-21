#r "Newtonsoft.Json"
#load "BasicLuisDialog.csx"

using System;
using System.Text;
using System.Net;
using System.Threading;
using Newtonsoft.Json;

using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    // Global values
    string strUserName = "";

    // Initialize the azure bot
    using (BotService.Initialize())
    {
        // Deserialize the incoming activity
        string jsonContent = await req.Content.ReadAsStringAsync();
        var activity = JsonConvert.DeserializeObject<Activity>(jsonContent);
        
        // authenticate incoming request and add activity.ServiceUrl to MicrosoftAppCredentials.TrustedHostNames
        // if request is authenticated
        if (!await BotService.Authenticator.TryAuthenticateAsync(req, new [] {activity}, CancellationToken.None))
        {
            return BotAuthenticator.GenerateUnauthorizedResponse(req);
        }
    
        if (activity != null)
        {
            var client = new ConnectorClient(new Uri(activity.ServiceUrl));
            // one of these will have an interface and process it
            switch (activity.GetActivityType())
            {
                case ActivityTypes.Message:
                    // Get any saved values
                    StateClient sc = activity.GetStateClient();
                    BotData userData = sc.BotState.GetPrivateConversationData(activity.ChannelId, activity.Conversation.Id, activity.From.Id);
                    strUserName = userData.GetProperty<string>("UserName") ?? "";

                    StringBuilder strReplyMessage = new StringBuilder();

                    if (strUserName == "") // Name was never provided
                    {
                        // If we have asked for a username but it has not been set
                        // the current response is the user name
                        strReplyMessage.Append($"안녕하세요 {activity.Text}학년 이군요!");
                        strReplyMessage.Append($"\n");
                        strReplyMessage.Append($"무엇이 궁금하나요?");


                        // Set BotUserData
                        userData.SetProperty<string>("UserName", activity.Text);
                        sc.BotState.SetPrivateConversationData(activity.ChannelId, activity.Conversation.Id, activity.From.Id, userData);

                        //send reply
                        ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                        Activity replyMessage = activity.CreateReply(strReplyMessage.ToString());
                        await connector.Conversations.ReplyToActivityAsync(replyMessage);

                    }
                    else // Name was provided
                    {
                        strReplyMessage.Append($"{strUserName}학년");
                        Activity replyMessage = activity.CreateReply(strReplyMessage.ToString());
                        await client.Conversations.ReplyToActivityAsync(replyMessage);

                        await Conversation.SendAsync(activity, () => new BasicLuisDialog());
                    }

                    break;
                case ActivityTypes.ConversationUpdate:
                    IConversationUpdateActivity update = activity;
                    if (update.MembersAdded.Any())
                    {
                        var reply = activity.CreateReply();
                        var newMembers = update.MembersAdded?.Where(t => t.Id != activity.Recipient.Id);
                        foreach (var newMember in newMembers)
                        {
                            reply.Text = "안녕하세요? 무엇이든지 물어볼 봇입니다!";
                            await client.Conversations.ReplyToActivityAsync(reply);
                            reply.Text = $"친구의 학년을 알고싶어요. 몇학년인지 숫자를 써주세요!";
                            await client.Conversations.ReplyToActivityAsync(reply);
                        }                     
                    }
                    break;
                case ActivityTypes.ContactRelationUpdate:
                case ActivityTypes.Typing:
                case ActivityTypes.DeleteUserData:
                case ActivityTypes.Ping:
                default:
                    log.Error($"Unknown activity type ignored: {activity.GetActivityType()}");
                    break;
            }
        }
        return req.CreateResponse(HttpStatusCode.Accepted);
    }    
}