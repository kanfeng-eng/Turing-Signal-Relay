namespace TuringSignalRelay
{
    public sealed class LiveFrameBuffer
    {
        public ObserverFrame Current
        {
            get; private set;
        }
        public string Status
        {
            get; private set;
        }
        public bool AcceptJson(string json)
        {
            var response = UnityEngine.JsonUtility.FromJson<ObserverSnapshotResponse>(json);
            if (response == null)
                throw new System.ArgumentException("Empty snapshot response.");
            if (response.snapshot == null)
                return Accept(UnityEngine.JsonUtility.FromJson<ObserverFrame>(json));
            if (response.protocolVersion != 1 || response.tick != response.snapshot.tick || string.IsNullOrWhiteSpace(response.status))
                throw new System.ArgumentException("Invalid snapshot response.");
            if (!Accept(response.snapshot))
                return false;
            Status = response.status;
            return true;
        }
        public readonly System.Collections.Generic.List<ObserverFrame> History = new System.Collections.Generic.List<ObserverFrame>();
        public void Reset()
        {
            Current = null;
            Status = null;
            History.Clear();
        }
        public bool Accept(ObserverFrame frame)
        {
            if (frame == null)
                throw new System.ArgumentNullException("frame");
            frame.Validate();
            if (Current != null && frame.tick <= Current.tick)
                return false;
            frame.ReuseUnchangedMap(Current);
            Current = frame;
            Status = "frame received";
            History.Add(frame);
            if (History.Count > 9001)
                History.RemoveAt(0);
            return true;
        }
    }
}
