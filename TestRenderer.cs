using System;
using System.Drawing.Imaging;
using System.IO;

namespace Book_Reader
{
    class TestRenderer
    {
        public static void RunTest()
        {
            try
            {
                string path = @"C:\Users\pit\Downloads\Dhenovel_Kurasu_de_Nibanme_V1_compressed.pdf";
                if (!File.Exists(path))
                {
                    Console.WriteLine("File not found: " + path);
                    return;
                }

                using var doc = PdfiumViewer.Core.PdfDocument.Load(path);
                var page = doc.Pages[0];
                var size = page.Size;
                using var image = page.Render((int)size.Width, (int)size.Height, 96, 96, PdfiumViewer.PdfRotation.Rotate0, PdfiumViewer.Enums.PdfRenderFlags.None);
                
                string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_render.png");
                image.Save(outPath, ImageFormat.Png);
                Console.WriteLine("Rendered to: " + outPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }
    }
}
