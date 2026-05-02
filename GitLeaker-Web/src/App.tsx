import { useState, useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";

interface CommitUpdate {
  commitHash: string;
  commitMessage: string;
  author: string;
  totalCommits: number;
  totalFiles: number;
}

export default function App() {
  const [repoPath, setRepoPath] = useState("");
  const [scanId, setScanId] = useState<string | null>(null);
  const [commits, setCommits] = useState<CommitUpdate[]>([]);
  const [status, setStatus] = useState<"idle" | "running" | "completed" | "failed">("idle");
  const [error, setError] = useState<string | null>(null);
  const hubRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    if (!scanId) return;

    const hub = new signalR.HubConnectionBuilder()
      .withUrl("http://localhost:5075/hubs/scan")
      .withAutomaticReconnect()
      .build();

    hub.on("CommitScanned", (update: CommitUpdate) => {
      setCommits((prev) => [update, ...prev]);
    });

    hub.on("ScanCompleted", () => {
      setStatus("completed");
      hub.stop();
    });

    hub.on("ScanFailed", ({ error }: { error: string }) => {
      setError(error);
      setStatus("failed");
      hub.stop();
    });

    hub.start()
      .then(() => hub.invoke("JoinScan", scanId))
      .catch((e) => console.error("SignalR connect failed:", e));

    hubRef.current = hub;
    return () => { hub.stop(); };
  }, [scanId]);

  const startScan = async () => {
    setCommits([]);
    setError(null);
    setStatus("running");

    const res = await fetch("http://localhost:5075/api/Scan/start", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({
        repoPath: null,
        repoUrl: repoPath,
        accessToken: null,
        provider: 0,
        branchFilter: null,
        daysBack: null,
        scanAllBranches: false,
        entropyThreshold: 3.5,
      }),
    });             

    const data = await res.json();
    setScanId(data.scanId);
  };

  return (
    <div style={{ fontFamily: "monospace", padding: "2rem", maxWidth: "800px", margin: "0 auto" }}>
      <h2>🔍 Secret Scanner</h2>

      <div style={{ display: "flex", gap: "8px", marginBottom: "1rem" }}>
        <input
          value={repoPath}
          onChange={(e) => setRepoPath(e.target.value)}
          placeholder="/home/user/my-repo"
          style={{ flex: 1, padding: "8px", fontFamily: "monospace" }}
        />
        <button
          onClick={startScan}
          disabled={status === "running" || !repoPath}
          style={{ padding: "8px 16px" }}
        >
          Start Scan
        </button>
      </div>

      {status === "running" && <p>⏳ Scanning...</p>}
      {status === "completed" && <p>✅ Scan completed! {commits.length} commits scanned.</p>}
      {status === "failed" && <p>❌ Failed: {error}</p>}

      <div style={{ marginTop: "1rem" }}>
        {commits.map((c, i) => (
          <div
            key={i}
            style={{
              padding: "8px",
              marginBottom: "4px",
              background: "#f5f5f5",
              borderLeft: "3px solid #333",
            }}
          >
            <span style={{ color: "#888" }}>{c.commitHash.slice(0, 7)}</span>
            {" — "}
            <span>{c.commitMessage}</span>
            <span style={{ float: "right", color: "#888" }}>
              {c.author} · {c.totalFiles} files
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}