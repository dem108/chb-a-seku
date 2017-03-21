#r "Newtonsoft.Json"

using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Web;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

[DataContract]
public class AccessTokenInfo
{
    [DataMember]
    public string access_token { get; set; }
    [DataMember]
    public string token_type { get; set; }
    [DataMember]
    public string expires_in { get; set; }
    [DataMember]
    public string scope { get; set; }
}

public class Authentication
{
    public static readonly string AccessUri = "https://oxford-speech.cloudapp.net/token/issueToken";
    private string clientId;
    private string clientSecret;
    private string request;
    private AccessTokenInfo token;
    private Timer accessTokenRenewer;

    //Access token expires everys 10 minutes. Renew it every 9 minutes only.
    private const int RefreshTokenDuration = 9;

    public Authentication(string clientId, string clientSecret)
    {
        this.clientId = clientId;
        this.clientSecret = clientSecret;

        //If clientid or client secret has special characters, encode before sending request
        this.request = string.Format("grant_type=client_credentials&client_id={0}&client_secret={1}&scope={2}",
                                          HttpUtility.UrlEncode(clientId),
                                          HttpUtility.UrlEncode(clientSecret),
                                          HttpUtility.UrlEncode("https://speech.platform.bing.com"));

        this.token = HttpPost(AccessUri, this.request);

        // renew the token every specfied minutes
        accessTokenRenewer = new Timer(new TimerCallback(OnTokenExpiredCallback),
                                       this,
                                       TimeSpan.FromMinutes(RefreshTokenDuration),
                                       TimeSpan.FromMilliseconds(-1));
    }

    //Return the access token
    public AccessTokenInfo GetAccessToken()
    {
        return this.token;
    }

    //Renew the access token
    private void RenewAccessToken()
    {
        AccessTokenInfo newAccessToken = HttpPost(AccessUri, this.request);
        //swap the new token with old one
        //Note: the swap is thread unsafe
        this.token = newAccessToken;
        Console.WriteLine(string.Format("Renewed token for user: {0} is: {1}",
                          this.clientId,
                          this.token.access_token));
    }
    //Call-back when we determine the access token has expired 
    private void OnTokenExpiredCallback(object stateInfo)
    {
        try
        {
            RenewAccessToken();
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format("Failed renewing access token. Details: {0}", ex.Message));
        }
        finally
        {
            try
            {
                accessTokenRenewer.Change(TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Failed to reschedule timer to renew access token. Details: {0}", ex.Message));
            }
        }
    }

    //Helper function to get new access token
    private AccessTokenInfo HttpPost(string accessUri, string requestDetails)
    {
        //Prepare OAuth request 
        WebRequest webRequest = WebRequest.Create(accessUri);
        webRequest.ContentType = "application/x-www-form-urlencoded";
        webRequest.Method = "POST";
        byte[] bytes = Encoding.ASCII.GetBytes(requestDetails);
        webRequest.ContentLength = bytes.Length;
        using (Stream outputStream = webRequest.GetRequestStream())
        {
            outputStream.Write(bytes, 0, bytes.Length);
        }
        using (WebResponse webResponse = webRequest.GetResponse())
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AccessTokenInfo));
            //Get deserialized object from JSON stream
            AccessTokenInfo token = (AccessTokenInfo)serializer.ReadObject(webResponse.GetResponseStream());
            return token;
        }
    }

    private string DoSpeechReco(Attachment attachment)
    {
        AccessTokenInfo token;
        string headerValue;
        // Note: Sign up at https://microsoft.com/cognitive to get a subscription key.  
        // Use the subscription key as Client secret below.
        Authentication auth = new Authentication("YOURUSERID", "<YOUR API KEY FROM MICROSOFT.COM/COGNITIVE");
        string requestUri = "https://speech.platform.bing.com/recognize";

        //URI Params. Refer to the Speech API documentation for more information.
        requestUri += @"?scenarios=smd";                                // websearch is the other main option.
        requestUri += @"&appid=D4D52672-91D7-4C74-8AD8-42B1D98141A5";   // You must use this ID.
        requestUri += @"&locale=ko-KR";                                 // read docs, for other supported languages. 
        requestUri += @"&device.os=wp7";
        requestUri += @"&version=3.0";
        requestUri += @"&format=json";
        requestUri += @"&instanceid=565D69FF-E928-4B7E-87DA-9A750B96D9E3";
        requestUri += @"&requestid=" + Guid.NewGuid().ToString();

        string host = @"speech.platform.bing.com";
        string contentType = @"audio/wav; codec=""audio/pcm""; samplerate=16000";
        var wav = HttpWebRequest.Create(attachment.ContentUrl);
        string responseString = string.Empty;

        try
        {
            token = auth.GetAccessToken();
            Console.WriteLine("Token: {0}\n", token.access_token);

            //Create a header with the access_token property of the returned token
            headerValue = "Bearer " + token.access_token;
            Console.WriteLine("Request Uri: " + requestUri + Environment.NewLine);

            HttpWebRequest request = null;
            request = (HttpWebRequest)HttpWebRequest.Create(requestUri);
            request.SendChunked = true;
            request.Accept = @"application/json;text/xml";
            request.Method = "POST";
            request.ProtocolVersion = HttpVersion.Version11;
            request.Host = host;
            request.ContentType = contentType;
            request.Headers["Authorization"] = headerValue;

            using (Stream wavStream = wav.GetResponse().GetResponseStream())
            {
                byte[] buffer = null;
                using (Stream requestStream = request.GetRequestStream())
                {
                    int count = 0;
                    do
                    {
                        buffer = new byte[1024];
                        count = wavStream.Read(buffer, 0, 1024);
                        requestStream.Write(buffer, 0, count);
                    } while (wavStream.CanRead && count > 0);
                    // Flush
                    requestStream.Flush();
                }
                //Get the response from the service.
                Console.WriteLine("Response:");
                using (WebResponse response = request.GetResponse())
                {
                    Console.WriteLine(((HttpWebResponse)response).StatusCode);
                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        responseString = sr.ReadToEnd();
                    }
                    Console.WriteLine(responseString);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            Console.WriteLine(ex.Message);
        }
        dynamic data = JObject.Parse(responseString);
        return data.header.name;
    }
}