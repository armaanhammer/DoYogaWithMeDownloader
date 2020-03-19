using HtmlAgilityPack;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DoYogaWithMe
{
    class Program
    {
        static List<string> videoList = new List<string>();
        static string sessName = "";
        static string sessValue = "";

        static string ssessName = "";
        static string ssessValue = "";
       
        static void Main(string[] args)
        {
            string email = "";
            string password = "";

            if (args.Length != 2)
            {
                Console.WriteLine("Username and password is required to download videos.");
                Console.WriteLine("Please type your email");
                email = Console.ReadLine();
                Console.WriteLine("Please type your password");
                password = Console.ReadLine();
            }
            else
            {
                email = args[0];
                password = args[1];
            }

            Console.WriteLine("Logging in...");
            if (!login(email, password))
            {
                Console.WriteLine("Error logging in. Make sure your email/password is correct.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.WriteLine("Logged in.");
            
            getVideos("https://www.doyogawithme.com/yoga-classes?page=0");
        }

        static void getVideos(string url)
        {
            var web = new HtmlWeb();
            var doc = web.Load(url);

            Console.WriteLine("Fetching Videos...");

            HtmlNodeCollection videos = doc.DocumentNode.SelectNodes("//div[@class='view-content']//div//div//span//a");

            foreach (var item in videos)
            {
                var vidinfo = getvideoInfo("https://www.doyogawithme.com" + item.GetAttributeValue("href", string.Empty));
                downloadVideo(vidinfo.Item1, vidinfo.Item2);
                ////downloadVideo("testvideo", "https://cfe8aff5b.lwcdn.com/hls/45b8f457-7cdd-4c43-a561-80262b4ded98");
            }

            if (doc.DocumentNode.SelectSingleNode("//ul[@class='pagination']//li[@class='next']//a") != null)
            {
                var nextpagenode = doc.DocumentNode.SelectSingleNode("//ul[@class='pagination']//li[@class='next']//a");
                string nextpageUrl = "https://www.doyogawithme.com" + nextpagenode.GetAttributeValue("href", string.Empty);
                getVideos(nextpageUrl);
            }
        }

        static Tuple<string, string> getvideoInfo(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            request.KeepAlive = true;
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4088.0 Safari/537.36";
            request.Headers.Set(HttpRequestHeader.Cookie, sessName + "=" + sessValue + "; " + ssessName + "=" + ssessValue + ";");

            var response = (HttpWebResponse)request.GetResponse();

            string responseString = "";
            using (Stream stream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                responseString = reader.ReadToEnd();
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(responseString);

            var videoTitle = doc.DocumentNode.SelectSingleNode("//article//div[@class='content']//h1").InnerText.Trim(new char[] { '\r', '\n', ' ' });

            var videoTeacher = doc.DocumentNode.SelectSingleNode("//article//div[@id='primary-info']//a//span[@class='info']//span[2]").InnerText.Trim(new char[] { '\r', '\n', ' ' });

            var playeridnode = doc.DocumentNode.SelectSingleNode("//*[@data-player-id]");
            HtmlAttribute attribute = playeridnode.Attributes["data-player-id"];
            string playerId = attribute.Value;

            var srcIdnode = doc.DocumentNode.SelectSingleNode("//*[@data-player-id]//script").InnerText;
            var thing = srcIdnode.Split(new string[] { "\"src\": \"" }, StringSplitOptions.None)[1];
            var srcID = thing.Split(new string[] { "\",\n" }, StringSplitOptions.None)[0];

            //string jsonUrl = "https://play.lwcdn.com/web/public/native/config/" + playerId + "/" + srcID; //unused but might be useful in the future. 
            string m3u8Url = "https://cfe8aff5b.lwcdn.com/hls/" + srcID; //not sure if this is a permanant working address or will change. 

            return Tuple.Create(videoTeacher + " - " + videoTitle, m3u8Url);
        }

        static bool login(string username, string password)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.doyogawithme.com/yoga_member/login");

                request.KeepAlive = true;
                request.Headers.Add("Origin", @"https://www.doyogawithme.com");
                request.ContentType = "application/x-www-form-urlencoded";
                request.Referer = "https://www.doyogawithme.com/yoga_member/login";
                request.Method = "POST";
                request.CookieContainer = new CookieContainer();
                request.AllowAutoRedirect = false;
                string body = @"name=" + username + "&pass=" + password + "&remember_me=1&form_build_id=&form_id=user_login&op=Log+in";
                byte[] postBytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = postBytes.Length;
                Stream stream = request.GetRequestStream();
                stream.Write(postBytes, 0, postBytes.Length);
                stream.Close();

                var response = (HttpWebResponse)request.GetResponse();

                response = (HttpWebResponse)request.GetResponse();


                foreach (Cookie cook in response.Cookies)
                {
                    if (cook.Name.StartsWith("SSESS"))
                    {
                        ssessName = cook.Name;
                        ssessValue = cook.Value;
                    }
                    else if (cook.Name.StartsWith("SESS"))
                    {
                        sessName = cook.Name;
                        sessValue = cook.Value;
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }
        
        static void downloadVideo(string title, string m3u8url)
        {
            var chunkInfo = videoStream(m3u8url);

            try
            {
                string fileName = title + ".ts";

                char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
                fileName = new string(fileName.Where(ch => !invalidFileNameChars.Contains(ch)).ToArray());

                if (File.Exists(fileName))
                {
                    Console.WriteLine("Video file " + fileName + " already exits, skipping download.");
                    return;
                }

                using (var outputStream = File.Create (fileName))
                {
                    using (WebClient client = new WebClient())
                    {
                        int totalTicks = chunkInfo.Count;

                        var options = new ProgressBarOptions
                        {
                            ForegroundColor = ConsoleColor.Yellow,
                            ForegroundColorDone = ConsoleColor.DarkGreen,
                            BackgroundColor = ConsoleColor.DarkGray,
                            BackgroundCharacter = '\u2593',
                            ProgressBarOnBottom = true
                        };

                        using (var pbar = new ProgressBar(totalTicks, "Downloading: " + fileName, options))
                        {
                            foreach (var chunk in chunkInfo)
                            {
                                using (MemoryStream stream = new MemoryStream(client.DownloadData(chunk)))
                                {
                                    stream.CopyTo(outputStream);
                                }

                                pbar.Tick();
                            }
                        }
                    }
                }
            }
            catch (AmbiguousMatchException)
            {
            }
        }
        
        static List<string> videoStream(string baseUrl)
        {
            string m3u8playlist = "";

            using (var client = new WebClient())
            {
                Stream playlist = client.OpenRead(baseUrl + "/playlist.m3u8");
                StreamReader file = new StreamReader(playlist);

                string line;
                while ((line = file.ReadLine()) != null)
                {
                    //loop through until the last stream, its usually the highest quality. 
                    if (line.Contains(".m3u8"))
                    {
                        m3u8playlist = baseUrl + "/" +  line;
                    }
                }
                file.Close();
            }


            List<string> tsChunks = new List<string>();

            using (var client = new WebClient())
            {
                Stream playlist = client.OpenRead(m3u8playlist);
                StreamReader file = new StreamReader(playlist);

                string line;
                while ((line = file.ReadLine()) != null)
                {
                     if (line.Contains(".ts"))
                    {
                        tsChunks.Add(baseUrl + "/" + line);
                    }
                }
                file.Close();
            }
            return tsChunks;
        }
    }
}
