
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Client
{
    public enum UploadStatus
    {
        Waiting,
        Uploading,
        Completed,
        Failed
    }

    public class UploadQueueItem
    {
        public ScannedFile File { get; set; }

        public UploadStatus Status { get; set; } = UploadStatus.Waiting;

        public double Progress { get; set; } = 0;

        public string ErrorMessage { get; set; } = string.Empty;

        public UploadQueueItem(ScannedFile file)
        {
            File = file;
        }
    }

    public class UploadQueue
    {
        private readonly ConcurrentQueue<UploadQueueItem> queue
            = new ConcurrentQueue<UploadQueueItem>();

        private readonly List<UploadQueueItem> allItems
            = new List<UploadQueueItem>();

        private readonly object lockObject = new object();

        public List<UploadQueueItem> GetAllItems()
        {
            lock (lockObject)
            {
                return new List<UploadQueueItem>(allItems);
            }
        }

        // Thêm một file vào hàng đợi
        public bool AddFile(ScannedFile file)
        {
            if (file == null || !file.IsValid)
            {
                return false;
            }

            UploadQueueItem item = new UploadQueueItem(file);

            lock (lockObject)
            {
                allItems.Add(item);
                queue.Enqueue(item);
            }

            return true;
        }

        public int AddFiles(IEnumerable<ScannedFile> files)
        {
            if (files == null)
            {
                return 0;
            }

            int addedCount = 0;

            foreach (ScannedFile file in files)
            {
                if (AddFile(file))
                {
                    addedCount++;
                }
            }

            return addedCount;
        }

        public bool TryGetNext(out UploadQueueItem item)
        {
            return queue.TryDequeue(out item);
        }

        public void UpdateStatus(
            UploadQueueItem item,
            UploadStatus status,
            string errorMessage = "")
        {
            if (item == null)
            {
                return;
            }

            lock (lockObject)
            {
                item.Status = status;
                item.ErrorMessage = errorMessage;

                if (status == UploadStatus.Completed)
                {
                    item.Progress = 100;
                }
            }
        }

        public void UpdateProgress(
            UploadQueueItem item,
            double progress)
        {
            if (item == null)
            {
                return;
            }

            lock (lockObject)
            {
                item.Progress = Math.Clamp(progress, 0, 100);
            }
        }

        public int WaitingCount
        {
            get
            {
                return queue.Count;
            }
        }
    }
}
