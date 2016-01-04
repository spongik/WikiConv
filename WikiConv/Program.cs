using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace WikiConv
{
    class WikiPage
    {
        public string Title;
        public string Text;

        public List<string> GetChildrenPages()
        {
            var result = new List<string>();

            if (!String.IsNullOrEmpty(Text))
            {
                int start = 0, end = 0, end2 = 0;
                string child;

                while ((start = Text.IndexOf("[[", start)) >= 0)
                {
                    end = Text.IndexOf("]]", start);
                    end2 = Text.IndexOf("|", start);
                    if (end >= 0)
                    {
                        end2 = (end2 >= 0 && end2 < end) ? end2 : end;
                        if (end2 - start > 2)
                        {
                            child = Text.Substring(start + 2, end2 - start - 2);
                            if (!result.Contains(child) && !child.StartsWith("Файл:"))
                            {
                                result.Add(child);
                            }
                        }
                        start = end;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return result;
        }
    }

    class Program
    {
        static IEnumerable<WikiPage> ReadNextPage(string inputFile)
        {
            WikiPage page = new WikiPage();
            var skipCurrentPage = true;

            using (XmlReader reader = XmlReader.Create(inputFile))
            {
                reader.MoveToContent();
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (skipCurrentPage && reader.Name != "page")
                        {
                            continue;
                        }

                        switch (reader.Name)
                        {
                            case "page":
                                if (!skipCurrentPage)
                                {
                                    yield return page;
                                }
                                page = new WikiPage();
                                skipCurrentPage = false;
                                break;
                            case "ns":
                                if (reader.ReadElementContentAsString() != "0")
                                {
                                    skipCurrentPage = true;
                                }
                                break;
                            case "title":
                                page.Title = reader.ReadElementContentAsString();
                                break;
                            case "text":
                                page.Text = reader.ReadElementContentAsString();
                                break;
                        }
                    }
                }
            }

            yield break;
        }

        static void Main(string[] args)
        {
            if (args.Count() < 1)
            {
                Console.WriteLine("Please specify input file");
                return;
            }

            string inputFile = args[0];
            string outputFile = "redis.txt";

            int processed = 0;

            using (StreamWriter outStream = new StreamWriter(outputFile, true))
            {
                foreach (var page in ReadNextPage(inputFile))
                {
                    var children = page.GetChildrenPages();

                    if (children.Count > 0)
                    {
                        outStream.Write(String.Format("*{0}\r\n$4\r\nSADD\r\n${1}\r\n{2}\r\n", 2 + children.Count, ASCIIEncoding.UTF8.GetByteCount(page.Title), page.Title));

                        foreach (var child in children)
                        {
                            outStream.Write(String.Format("${0}\r\n{1}\r\n", ASCIIEncoding.UTF8.GetByteCount(child), child));
                        }
                    }

                    processed++;

                    if (processed % 1000 == 0)
                    {
                        Console.Write("\rDone {0} items", processed);
                    }
                }

                Console.WriteLine("\r\nExiting");
            }
        }
    }
}
