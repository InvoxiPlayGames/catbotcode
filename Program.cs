using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace catbotcode
{
    class Program
    {
        static Dictionary<string, string> config = new Dictionary<string, string>();
        static HttpClient client = new HttpClient();
        static Random rng = new Random();
        static HttpStatusCode LastResponseCode; // oh no

        // crappy helper functions to GET/POST request
        // i am very stubborn and i do not want to bother with real async so this works
        // TODO: please fix this
        static string GETRequest(string URL)
        {
            Task<HttpResponseMessage> reqtask = client.GetAsync(URL);
            reqtask.Wait();
            HttpResponseMessage response = reqtask.Result;
            Task<string> contenttask = response.Content.ReadAsStringAsync();
            contenttask.Wait();
            string content = contenttask.Result;
            LastResponseCode = response.StatusCode;
            return content;
        }

        static string POSTRequest(string URL, HttpContent postdata)
        {
            Task<HttpResponseMessage> reqtask = client.PostAsync(URL, postdata);
            reqtask.Wait();
            HttpResponseMessage response = reqtask.Result;
            Task<string> contenttask = response.Content.ReadAsStringAsync();
            contenttask.Wait();
            string content = contenttask.Result;
            LastResponseCode = response.StatusCode;
            return content;
        }

        static void Main(string[] args)
        {
            // read config file
            string configpath = args.Length >= 1 ? args[0] : "./config.ini";
            if (!File.Exists(configpath))
            {
                Console.WriteLine($"Error: config file at {configpath} could not be found.");
                return;
            }
            string[] configtext = File.ReadAllLines(configpath);
            foreach(string configline in configtext)
            {
                string[] opts = configline.Split('=');
                if (opts.Length == 2 && opts[0][0] != '#')
                {
                    config.Add(opts[0], opts[1]);
                }
            }

            // verify that everything needed is set in the config
            if (!config.ContainsKey("AccessToken") ||
                !config.ContainsKey("PicturesPath") ||
                !config.ContainsKey("InstanceBase") ||
                !(config.ContainsKey("PostDelay") || (config.ContainsKey("PostDelayStart") && config.ContainsKey("PostDelayEnd"))))
            {
                Console.WriteLine($"Error: config file is missing required values.");
                Console.WriteLine($"       required values are; AccessToken, PicturesPath, InstanceBase,");
                Console.WriteLine($"       and either PostDelay or PostDelayStart and PostDelayEnd");
                return;
            }

            // set up the HttpClient
            client.BaseAddress = new Uri(config["InstanceBase"]);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["AccessToken"]);

            // test that our account's token is real
            string accountjson = GETRequest("/api/v1/accounts/verify_credentials");
            if (LastResponseCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: got response code of {LastResponseCode} trying to fetch the account.");
                Console.WriteLine($"       is the AccessToken still valid?");
                return;
            }
            Account account = JsonSerializer.Deserialize<Account>(accountjson);
            Console.WriteLine($"Logged in as {account.display_name} (@{account.username})");

            // enter the bot loop
            while (true)
            {
                // select a random file to upload from the path
                string[] possiblefiles = Directory.GetFiles(config["PicturesPath"]);
                int randomIndex = rng.Next(possiblefiles.Length);
                string filepath = possiblefiles[randomIndex];
                string filename = Path.GetFileName(filepath);

                // upload the file
                Console.WriteLine($"Uploading file {filename}...");
                byte[] imagefile = File.ReadAllBytes(filepath);
                MultipartFormDataContent multipartContent = new MultipartFormDataContent();
                multipartContent.Add(new ByteArrayContent(imagefile), "\"file\"", $"\"{filename}\"");
                string mediajson = POSTRequest("/api/v1/media", multipartContent);
                if (LastResponseCode != HttpStatusCode.OK && LastResponseCode != HttpStatusCode.Created) // certain versions of the mastodon API will return created
                {
                    Console.WriteLine($"ERROR! Failed to upload the attachment. HTTP Code: {LastResponseCode}");
                    Console.WriteLine(mediajson);
                    goto botsleep;
                }
                Attachment attachment = new Attachment();
                try
                {
                    attachment = JsonSerializer.Deserialize<Attachment>(mediajson);
                } catch (Exception ex)
                {
                    Console.WriteLine($"ERROR! An exception was caught while parsing the media JSON. Exception: {ex.Message}");
                    Console.WriteLine(mediajson);
                    goto botsleep;
                }
                string mediaid = attachment.id;
                Console.WriteLine($"Uploaded {filename} (Media ID: {mediaid})");

                // post the toot
                Dictionary<string, string> postopts = new Dictionary<string, string>();
                postopts["status"] = "";
                if (config.ContainsKey("Text"))
                {
                    postopts["status"] += config["Text"] + "\n";
                }
                if (config.ContainsKey("PostFilename") && config["PostFilename"].ToLower() == "true")
                {
                    postopts["status"] += filename + "\n";
                }
                postopts["media_ids[]"] = mediaid;
                string statusjson = POSTRequest("/api/v1/statuses", new FormUrlEncodedContent(postopts));
                if (LastResponseCode != HttpStatusCode.OK && LastResponseCode != HttpStatusCode.Created) // certain versions of the mastodon API will return created
                {
                    Console.WriteLine($"ERROR! Failed to post the toot. HTTP Code: {LastResponseCode}");
                    Console.WriteLine(statusjson);
                    goto botsleep;
                }
                Status status = new Status();
                try
                {
                    status = JsonSerializer.Deserialize<Status>(statusjson);
                } catch (Exception ex)
                {
                    Console.WriteLine($"ERROR! An exception was caught while parsing the status JSON. Exception: {ex.Message}");
                    Console.WriteLine(statusjson);
                    goto botsleep;
                }
                Console.WriteLine($"Posted toot! {status.url}");

            botsleep:
                int postdelay = 1800;
                if (config.ContainsKey("PostDelay"))
                {
                    postdelay = int.Parse(config["PostDelay"]);
                } else
                {
                    int postlo = int.Parse(config["PostDelayStart"]);
                    int posthi = int.Parse(config["PostDelayEnd"]);
                    postdelay = rng.Next(postlo, posthi);
                }
                Console.WriteLine($"Sleeping for {postdelay} seconds... gn");
                Thread.Sleep(postdelay * 1000);
            }
        }
    }
}
