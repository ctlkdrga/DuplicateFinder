using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using NLog;

namespace Gbd.Sandbox.DuplicateFinder.Model
{
    public class FileSearcher
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();    

        private DirectoryInfo _baseDirectory;
        private ICollection<DupeFileInfo> _fileList;
        private IDictionary<HashingType, bool> _allHashesDoneByType;
        private SimilarFileSet _similars;


        public FileSearcher()
        {
            Initialize();
        }

        private void Initialize()
        {
            _baseDirectory = null;
            _fileList = new List<DupeFileInfo>();
            _similars = null;

            _allHashesDoneByType = new Dictionary<HashingType, bool>();
            _allHashesDoneByType.Add(HashingType.FullHashing, false);
            _allHashesDoneByType.Add(HashingType.QuickHashing, false);
            _allHashesDoneByType.Add(HashingType.SizeHashing, false);
        }


        public FileSearcher Reset()
        {
            Initialize();
            _log.Info("Reset of FileSearcher successful");
            return this;
        }


        public FileSearcher SetDirectory(string newDirectory)
        {
            _baseDirectory = new DirectoryInfo(newDirectory);

            _log.Info("FileSearcher now working in directory '{0}'", _baseDirectory.FullName);

            return this;
        }

        public FileSearcher BuildFileList(FileSearchOption options)
        {
            _log.Info("Start building file list");

            foreach (var curFile in 
                        _baseDirectory.GetFiles("*", SearchOption.AllDirectories)
                        .Where(file => file.Exists))
            {
                var info = new DupeFileInfo(curFile);
                _fileList.Add(info);
            }

            _log.Info("Found {0} files in search directory", _fileList.Count);

            if ((options & FileSearchOption.BgComputeHash) != 0)
            {
                _log.Trace("Option BgComputeHash is set: launching BG hashing");
                // TODO: start hashing as soon as file is known (Size hashing should not generate IOs)
                this.StartBgHashing();
            }

            return this;
        }

        public FileSearcher CompareHashes(HashingType hashingType)
        {

            _log.Warn("*** SLEEPING TO WAIT FOR BG HASH COMPLETION ***");
            System.Threading.Thread.Sleep(100);
            _log.Warn("*** WAKING FROM SLEEP ***");


            _log.Debug("Start building sorted list for {0} hashing", hashingType);

            _similars = new SimilarFileSet(_fileList, hashingType);

            return this;
        }

        private void StartBgHashing()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(ProcessHashing);
            // TODO: report progress delegate
            worker.RunWorkerAsync();
        }

        public void ProcessHashing(object sender, DoWorkEventArgs e)
        {
            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "ProcessHashing";
            }

            _log.Info("Start (normally BG) routine ProcessHashing");

            ComputeHashes(HashingType.SizeHashing);
            ComputeHashes(HashingType.QuickHashing);
            ComputeHashes(HashingType.FullHashing);



            // TODO: report progress
            // TODO: deal with cancellation
        }

        private void ComputeHashes(HashingType hashingType)
        {
            _log.Debug("Start computing {1} hashes for all {0} known files", _fileList.Count, hashingType);

            foreach (var file in _fileList.Where(f => f.GetOrComputeHash(hashingType) == null))
            {
                file.GetOrComputeHash(hashingType);
            }

            _allHashesDoneByType[hashingType] = true;
            _log.Debug("Done computing {1} hashes for all {0} known files", _fileList.Count, hashingType);
        }


  
    }
}