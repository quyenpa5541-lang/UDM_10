using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Client
{
    public enum UploadStatus
    {
        Waiting,
        Uploading,
        Completed,
        Failed,
        Cancelled
    }

    public class UploadQueueItem
    {
        private readonly object sync = new object();
        private UploadStatus status = UploadStatus.Waiting;
        private double progress;
        private string errorMessage = string.Empty;

        public ScannedFile Source { get; }

        public UploadStatus Status
        {
            get { lock (sync) { return status; } }
        }

        public double Progress
        {
            get { lock (sync) { return progress; } }
        }

        public string ErrorMessage
        {
            get { lock (sync) { return errorMessage; } }
        }

        public UploadQueueItem(ScannedFile source)
        {
            Source = source;
        }

        internal void SetState(
            UploadStatus newStatus,
            string error,
            double? newProgress)
        {
            lock (sync)
            {
                status = newStatus;
                errorMessage = error;

                if (newProgress.HasValue)
                {
                    progress = newProgress.Value;
                }
            }
        }

        internal void SetProgress(double value)
        {
            lock (sync)
            {
                progress = value;
            }
        }
    }

    public class UploadQueue
    {
        private readonly object lockObject = new object();

        private readonly Queue<UploadQueueItem> pending
            = new Queue<UploadQueueItem>();

        private readonly List<UploadQueueItem> allItems
            = new List<UploadQueueItem>();

        private readonly HashSet<string> activePaths
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int waitingCount;

        public event Action<UploadQueueItem>? ItemChanged;

        public List<UploadQueueItem> GetAllItems()
        {
            lock (lockObject)
            {
                return new List<UploadQueueItem>(allItems);
            }
        }

        public bool AddFile(ScannedFile? file)
        {
            if (file == null || !file.IsValid)
            {
                return false;
            }

            UploadQueueItem item = new UploadQueueItem(file);

            lock (lockObject)
            {
                if (!activePaths.Add(file.FullPath))
                {
                    return false;
                }

                allItems.Add(item);
                pending.Enqueue(item);
                waitingCount++;
            }

            OnItemChanged(item);
            return true;
        }

        public int AddFiles(IEnumerable<ScannedFile>? files)
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

        public bool TryGetNext([MaybeNullWhen(false)] out UploadQueueItem item)
        {
            UploadQueueItem? next = null;

            lock (lockObject)
            {
                while (pending.Count > 0)
                {
                    UploadQueueItem candidate = pending.Dequeue();

                    if (candidate.Status != UploadStatus.Waiting)
                    {
                        continue;
                    }

                    ChangeStatus(candidate, UploadStatus.Uploading, string.Empty);
                    next = candidate;
                    break;
                }
            }

            if (next == null)
            {
                item = default;
                return false;
            }

            OnItemChanged(next);
            item = next;
            return true;
        }

        public void UpdateStatus(
            UploadQueueItem? item,
            UploadStatus status,
            string errorMessage = "")
        {
            if (item == null || status == UploadStatus.Waiting)
            {
                return;
            }

            lock (lockObject)
            {
                UploadStatus current = item.Status;

                if (current != UploadStatus.Waiting &&
                    current != UploadStatus.Uploading)
                {
                    return;
                }

                ChangeStatus(
                    item,
                    status,
                    status == UploadStatus.Failed ? errorMessage : string.Empty);
            }

            OnItemChanged(item);
        }

        public void UpdateProgress(
            UploadQueueItem? item,
            double progress)
        {
            if (item == null || double.IsNaN(progress))
            {
                return;
            }

            lock (lockObject)
            {
                if (item.Status != UploadStatus.Uploading)
                {
                    return;
                }

                item.SetProgress(Math.Clamp(progress, 0, 100));
            }

            OnItemChanged(item);
        }

        public bool Remove(UploadQueueItem? item)
        {
            if (item == null)
            {
                return false;
            }

            lock (lockObject)
            {
                if (item.Status != UploadStatus.Waiting)
                {
                    return false;
                }

                ChangeStatus(item, UploadStatus.Cancelled, string.Empty);
                allItems.Remove(item);
            }

            OnItemChanged(item);
            return true;
        }

        public bool Retry(UploadQueueItem? item)
        {
            if (item == null)
            {
                return false;
            }

            lock (lockObject)
            {
                if (item.Status != UploadStatus.Failed ||
                    !allItems.Contains(item) ||
                    activePaths.Contains(item.Source.FullPath))
                {
                    return false;
                }

                ChangeStatus(item, UploadStatus.Waiting, string.Empty);
                pending.Enqueue(item);
            }

            OnItemChanged(item);
            return true;
        }

        public int RetryFailed()
        {
            int retried = 0;

            foreach (UploadQueueItem item in GetAllItems())
            {
                if (Retry(item))
                {
                    retried++;
                }
            }

            return retried;
        }

        public int ClearFinished()
        {
            lock (lockObject)
            {
                return allItems.RemoveAll(item =>
                    item.Status == UploadStatus.Completed ||
                    item.Status == UploadStatus.Failed ||
                    item.Status == UploadStatus.Cancelled);
            }
        }

        public int WaitingCount
        {
            get
            {
                lock (lockObject)
                {
                    return waitingCount;
                }
            }
        }

        public int ActiveCount
        {
            get
            {
                lock (lockObject)
                {
                    return activePaths.Count;
                }
            }
        }

        private static bool IsActive(UploadStatus status)
        {
            return status == UploadStatus.Waiting ||
                   status == UploadStatus.Uploading;
        }

        private void ChangeStatus(
            UploadQueueItem item,
            UploadStatus newStatus,
            string error)
        {
            UploadStatus oldStatus = item.Status;

            if (oldStatus == UploadStatus.Waiting &&
                newStatus != UploadStatus.Waiting)
            {
                waitingCount--;
            }
            else if (oldStatus != UploadStatus.Waiting &&
                     newStatus == UploadStatus.Waiting)
            {
                waitingCount++;
            }

            if (IsActive(oldStatus) && !IsActive(newStatus))
            {
                activePaths.Remove(item.Source.FullPath);
            }
            else if (!IsActive(oldStatus) && IsActive(newStatus))
            {
                activePaths.Add(item.Source.FullPath);
            }

            double? newProgress = newStatus switch
            {
                UploadStatus.Completed => 100,
                UploadStatus.Waiting => 0,
                _ => null
            };

            item.SetState(newStatus, error, newProgress);
        }

        private void OnItemChanged(UploadQueueItem item)
        {
            ItemChanged?.Invoke(item);
        }
    }
}
