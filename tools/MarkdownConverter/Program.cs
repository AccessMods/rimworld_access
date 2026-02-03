using System;
using System.IO;
using Markdig;

namespace MarkdownConverter
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: MarkdownConverter <input.md> <output.html>");
                return 1;
            }

            string inputFile = args[0];
            string outputFile = args[1];

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: Input file '{inputFile}' not found.");
                return 1;
            }

            try
            {
                // Read markdown content
                string markdown = File.ReadAllText(inputFile);

                // Configure Markdig pipeline with GitHub-flavored markdown
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();

                // Convert markdown to HTML
                string htmlBody = Markdown.ToHtml(markdown, pipeline);

                // Create complete HTML document with styling
                string htmlDocument = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>RimWorld Access - README</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            line-height: 1.6;
            max-width: 900px;
            margin: 0 auto;
            padding: 20px;
            color: #333;
            background-color: #fff;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
        }}
        h1 {{
            font-size: 2em;
            border-bottom: 1px solid #eaecef;
            padding-bottom: 0.3em;
        }}
        h2 {{
            font-size: 1.5em;
            border-bottom: 1px solid #eaecef;
            padding-bottom: 0.3em;
        }}
        h3 {{ font-size: 1.25em; }}
        h4 {{ font-size: 1em; }}
        h5 {{ font-size: 0.875em; }}
        h6 {{ font-size: 0.85em; color: #6a737d; }}
        code {{
            background-color: #f6f8fa;
            padding: 0.2em 0.4em;
            margin: 0;
            border-radius: 3px;
            font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
            font-size: 85%;
        }}
        pre {{
            background-color: #f6f8fa;
            padding: 16px;
            border-radius: 6px;
            overflow: auto;
            font-size: 85%;
            line-height: 1.45;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
            margin: 0;
            border-radius: 0;
            font-size: 100%;
        }}
        a {{
            color: #0366d6;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 16px 0;
            display: block;
            overflow: auto;
        }}
        th, td {{
            border: 1px solid #dfe2e5;
            padding: 6px 13px;
        }}
        th {{
            background-color: #f6f8fa;
            font-weight: 600;
        }}
        tr {{
            background-color: #fff;
            border-top: 1px solid #c6cbd1;
        }}
        tr:nth-child(2n) {{
            background-color: #f6f8fa;
        }}
        blockquote {{
            border-left: 4px solid #dfe2e5;
            padding: 0 1em;
            color: #6a737d;
            margin: 0;
        }}
        img {{
            max-width: 100%;
            box-sizing: content-box;
        }}
        ul, ol {{
            padding-left: 2em;
            margin-top: 0;
            margin-bottom: 16px;
        }}
        li + li {{
            margin-top: 0.25em;
        }}
        hr {{
            height: 0.25em;
            padding: 0;
            margin: 24px 0;
            background-color: #e1e4e8;
            border: 0;
        }}
        kbd {{
            display: inline-block;
            padding: 3px 5px;
            font: 11px monospace;
            line-height: 10px;
            color: #444d56;
            vertical-align: middle;
            background-color: #fafbfc;
            border: solid 1px #d1d5da;
            border-bottom-color: #c6cbd1;
            border-radius: 3px;
            box-shadow: inset 0 -1px 0 #c6cbd1;
        }}
    </style>
</head>
<body>
{htmlBody}
</body>
</html>";

                // Write HTML file
                File.WriteAllText(outputFile, htmlDocument);

                Console.WriteLine($"Successfully converted {inputFile} to {outputFile}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
