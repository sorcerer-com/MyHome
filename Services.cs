using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;

using Newtonsoft.Json.Linq;

using NLog;

using ScrapySharp.Html.Forms;
using ScrapySharp.Network;

namespace MyHome
{
    public static class Services
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public static bool SendEMail(string server, string sender, string password,
            string recipient, string subject, string content, List<string> fileNames = null)
        {
            // TODO: need to be tested
            try
            {
                logger.Info($"Send email to '{recipient}' subject: '{subject}'");

                var mail = new MailMessage(sender, recipient, subject, content);
                fileNames?.ForEach(f => mail.Attachments.Add(new Attachment(f)));

                string host = server;
                int port = 465;
                if (server.Contains(":"))
                {
                    host = server.Split(':')[0];
                    port = int.Parse(server.Split(':')[1]);
                }
                using var smtp = new SmtpClient(host, port)
                {
                    Timeout = 20 * 1000, // 20 sec
                    Credentials = new NetworkCredential(sender, password)
                };
                smtp.EnableSsl = (smtp.Port == 465);
                smtp.Send(mail);

                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, $"Cannot send email to '{recipient}' subject: '{subject}'");
                return false;
            }
        }

        public static bool SendSMS(string number, string provider, string password, string message)
        {
            try
            {
                logger.Info($"Send SMS '{message.Replace("\n", " ")}' to {number}");
                if (provider.ToLower() == "telenor")
                {
                    var browser = new ScrapingBrowser { Encoding = Encoding.UTF8 };
                    var page = browser.NavigateToPage(new Uri("https://my.telenor.bg"));
                    // login
                    var form = new PageWebForm(page.Find("form", ScrapySharp.Html.By.Class("form")).First(), browser);
                    form["account"] = number[1..];
                    page = form.Submit(new Uri("https://id.telenor.bg/id/signin-switchable/"));
                    form = new PageWebForm(page.Find("form", ScrapySharp.Html.By.Class("form")).First(), browser);
                    form["pin"] = password;
                    page = form.Submit(new Uri("https://id.telenor.bg/id/verify-phone/"));
                    // go to sms
                    page = browser.NavigateToPage(new Uri("https://my.telenor.bg/compose"));
                    // sms
                    form = page.FindFormById("new-sms-form");
                    form["receiverPhoneNum"] = number;
                    if (message.Length > 99)
                        message = message[..99];
                    form.FormFields.Add(new FormField { Name = "txtareaMessage", Value = message });
                    try
                    {
                        page = form.Submit(new Uri("https://my.telenor.bg/st/validatesms"));
                    }
                    catch (Exception e)
                    {
                        // Expected exception - response 302 Found
                        if (!(e.InnerException is WebException))
                            throw;
                    }
                    browser.NavigateToPage(new Uri("https://my.telenor.bg/logout"));
                    return true;
                }
                logger.Error($"Cannot send SMS - invalid operator '{provider}'");
                return false;
            }
            catch (Exception e)
            {
                logger.Error(e, $"Cannot send SMS '{message.Replace("\n", " ")}' to {number}");
                return false;
            }
        }

        public static JToken GetJsonContent(string url)
        {
            try
            {
                logger.Debug($"Get json content from '{url}'");
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                // TODO: simple result
                //var json = client.GetStringAsync(url).Result;
                var json = "[{\"name\": \"Motion\", \"value\": false, \"aggrType\": \"avg\", \"desc\": \"Motion detection\"},{\"name\": \"Temperature\", \"value\": 24.00, \"aggrType\": \"avg\", \"desc\": \"Current temperature\"},{\"name\": \"Humidity\", \"value\": 48.00, \"aggrType\": \"avg\", \"desc\": \"Current humidity\"},{\"name\": \"Smoke\", \"value\": 35.00, \"aggrType\": \"avg\", \"desc\": \"Smoke detection\"},{\"name\": \"Lighting\", \"value\": 52.00, \"aggrType\": \"avg\", \"desc\": \"Current lighting\"}]";

                return JToken.Parse(json);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Cannot get json content from '{url}'");
                return null;
            }
        }
    }
}
