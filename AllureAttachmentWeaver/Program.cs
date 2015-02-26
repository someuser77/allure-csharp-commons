using System;
using System.IO;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using log4net.Config;
using log4net;
using AllureCSharpCommons;
using Mono.Collections.Generic;
using System.Reflection;
using System.Linq;
using log4net.Core;
using log4net.Appender;
using log4net.Layout;
using Mono.Cecil.Pdb;

namespace AllureAttachmentWeaver
{
    class MainClass
    {
        private static readonly ILog logger = LogManager.GetLogger("AllureAttachmentWeaverLogger");
        
        // the PDB rewriting is based on this post:
        // https://johnhmarks.wordpress.com/2011/01/19/getting-mono-cecil-to-rewrite-pdb-files-to-enable-debugging/
        
        /// <summary>
        /// The entry point of the program, where the program control starts and ends.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        public static void Main(string[] args)
        {
            Level loggingLevel = Level.Info;
            
            if (args.Length == 0)
            {
                logger.Fatal("You must specify the test assembly as an argument.");       
                return;
            }
            
            if (args.Length > 1 && args.Contains("-v"))
            {
                loggingLevel = Level.Debug;
            }
            
            // vNext should use XmlConfigurator with a logger.config file or app.config.
            SetupLogging(loggingLevel);
            
            string fileName = args[0];
            
            ReaderParameters readerParameters = GetReaderParameters(fileName);            
            
            WriterParameters writerParameters = new WriterParameters();
            
            if (PdbFileExists(fileName))
            {
                PdbReaderProvider pdbReaderProvider = new PdbReaderProvider();
                readerParameters.SymbolReaderProvider = pdbReaderProvider;
                readerParameters.ReadSymbols = true;
                writerParameters.WriteSymbols = true;
            }
            
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(fileName, readerParameters);

            IMethodWeaver weaver = new AttachmentWeaver();

            try
            {
                Weave(assembly, weaver);

                Save(assembly, writerParameters, new OverwriteTarget(fileName));
            }
            catch(Exception e)
            {
                logger.Error("Error writing assembly.", e);
            }
        }

        private static void Weave(AssemblyDefinition assembly, IMethodWeaver weaver)
        {
            // because we might be adding types/methods the enumerations must be eager.
            foreach (ModuleDefinition module in assembly.Modules.ToList())
            {
                foreach (TypeDefinition type in module.Types.ToList())
                {
                    if (type.FullName == "<Module>")
                        continue;

                    logger.Debug("Found type: " + type.FullName);

                    foreach (MethodDefinition method in type.Methods.ToList())
                    {
                        weaver.Weave(method);
                    }
                }
            }
        }

        private static void Save(AssemblyDefinition assembly, WriterParameters writerParameters, OutputStrategy outputStrategy)
        {
            string target = outputStrategy.PrepareTarget();
                        
            logger.Debug("Saving assembly to: " + target);

            assembly.Write(target, writerParameters);
            
            logger.Info("Assembly saved to: " + target);
        }

        private abstract class OutputStrategy
        {
            protected string mFile;
            protected string mPdb;
            protected bool mPdbExists;
            
            public OutputStrategy(string file)
            {
                mFile = file;
                mPdb = GetMatchingPdb(file);
                mPdbExists = mPdb != null;
            }
            
            public abstract string PrepareTarget();
        }
        
        private class OverwriteTarget : OutputStrategy
        {
            public OverwriteTarget(string file)
                : base(file) { }
               
            public override string PrepareTarget()
            {
                string backup = GetFileNameWithKeyword(mFile, "original");
                string original = mFile;
                
                CleanMove(original, backup);

                if (mPdbExists)
                {
                    string backupPdb = Path.ChangeExtension(backup, "pdb");
                    string originalPdb = mPdb;
                    CleanMove(originalPdb, backupPdb);
                }

                return mFile;
            }
        }
        
        private class DontOverwriteTarget : OutputStrategy
        {
            public DontOverwriteTarget(string file)
                : base(file) { }
            
            public override string PrepareTarget()
            {
                string generated = GetFileNameWithKeyword(mFile, "generated");

                if (File.Exists(generated))
                {
                    File.Delete(generated);
                }

                if (mPdbExists)
                {
                    string generatedPdb = Path.ChangeExtension(generated, "pdb");

                    if (File.Exists(generatedPdb))
                    {
                        File.Delete(generatedPdb);
                    }
                }

                return generated;
            }
        }
        
        static ReaderParameters GetReaderParameters(string fileName)
        {
            DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
            
            string assemblyLocation = Path.GetDirectoryName(fileName);
            if (assemblyLocation == String.Empty)
                assemblyLocation = ".";
            
            assemblyResolver.AddSearchDirectory(assemblyLocation);
            
            ReaderParameters readerParameters = new ReaderParameters();
            readerParameters.AssemblyResolver = assemblyResolver;
            
            return readerParameters;
        }

        private static bool PdbFileExists(string file)
        {
            return GetMatchingPdb(file) != null;
        }
        
        private static string GetMatchingPdb(string file)
        {
            string pdb = Path.ChangeExtension(file, "pdb");
            
            if (File.Exists(pdb))
                return pdb;
            
            return null;
        }
        
        private static bool TryGetMatchingPdb(string file, out string pdb)
        {
            pdb = GetMatchingPdb(file);
            return pdb == null;
        }
        
        private static void CleanMove(string source, string destination)
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(source, destination);
        }
        
        private static string GetFileNameWithKeyword(string filePath, string keyword)
        {
            string backup = Path.GetFileNameWithoutExtension(filePath) + "." + keyword + Path.GetExtension(filePath);
            return Path.Combine(Path.GetDirectoryName(filePath), backup);
        }
        
        private static void SetupLogging(Level level)
        {
            ConsoleAppender appender = new ConsoleAppender();
            appender.Layout = new SimpleLayout();
            appender.Threshold = level;
            BasicConfigurator.Configure(appender);
        }
    }
}
