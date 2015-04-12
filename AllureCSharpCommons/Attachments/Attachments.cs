using System;
using log4net;
using System.IO;
using AllureCSharpCommons.Utils;
using System.Text;

namespace AllureCSharpCommons
{
    public static class Attachments
    {
        private static ILog logger = LogManager.GetLogger(typeof(Attachments));
        
        private static Encoding defaultTextEncoding = new UTF8Encoding(false);
        
        public static event EventHandler<AttachmentAddedEventArgs> Added;

        public static bool OnAdded(string mimeType, string title, object attachment, object context)
        {
            EventHandler<AttachmentAddedEventArgs> added = Added;
            
            logger.Debug("Inside Add method...");
            
            if (added != null)
            {
                added(null, new AttachmentAddedEventArgs(mimeType, title, attachment, context));
                return true;
            }
            
            logger.Debug("The Added event was empty.");
            return false;
        }
        
        public static byte[] ToBinary(string text)
        {
            return defaultTextEncoding.GetBytes(text);
        }
        
        public static string Write(string text, string mimeType, string path)
        {
            return Write(ToBinary(text), mimeType, path);
        }

        public static string Write(byte[] bytes, string mimeType, string path)
        {
            string fullName = Path.Combine(path, GetFileName() + "." + MimeTypes.ToExtension(mimeType));

            File.WriteAllBytes(fullName, bytes);

            return path;
        }

        private static string GetFileName()
        {
            // generate a short hash for file names because too long hashs might
            // reach the limit of windows paths.
            
            // we shouldn't have any colissions under the assumption the number of different
            // attachments will be limited.
            
            // in case a true random hash will be needed the crypto API should be used.
            return Guid.NewGuid().GetHashCode().ToString();
        }
    }
}

