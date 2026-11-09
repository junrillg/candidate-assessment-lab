import { useEffect, useRef, useState } from "react";
import { message } from "./api";

export function useRecorder(onComplete: (blob: Blob) => Promise<void> | void) {
  const [recording, setRecording] = useState(false);
  const [seconds, setSeconds] = useState(0);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const recorder = useRef<MediaRecorder | undefined>(undefined);
  const stream = useRef<MediaStream | undefined>(undefined);
  const timer = useRef<ReturnType<typeof setInterval> | undefined>(undefined);
  const finish = useRef(onComplete);
  finish.current = onComplete;
  const mounted = useRef(true);
  const stop = () => {
    clearInterval(timer.current);
    if (recorder.current?.state === "recording") {
      if (mounted.current) setBusy(true);
      recorder.current.stop();
    }
    stream.current?.getTracks().forEach((track) => track.stop());
    if (mounted.current) setRecording(false);
  };
  const start = async () => {
    if (busy || recording) return;
    setError("");
    setBusy(true);
    try {
      if (
        !navigator.mediaDevices?.getUserMedia ||
        typeof MediaRecorder === "undefined"
      )
        throw new Error(
          "This browser cannot record audio. Use a current browser with microphone support.",
        );
      stream.current = await navigator.mediaDevices.getUserMedia({
        audio: true,
      });
      if (!mounted.current) {
        stream.current.getTracks().forEach((track) => track.stop());
        return;
      }
      const mimeType = [
        "audio/webm;codecs=opus",
        "audio/ogg;codecs=opus",
        "audio/mp4",
      ].find((type) => MediaRecorder.isTypeSupported(type));
      if (!mimeType)
        throw new Error(
          "No supported recording format was found. Try a different browser.",
        );
      const media = new MediaRecorder(stream.current, {
        mimeType,
        audioBitsPerSecond: 64000,
      });
      recorder.current = media;
      const chunks: BlobPart[] = [];
      const completed = finish.current;
      media.ondataavailable = (event) => {
        if (event.data.size) chunks.push(event.data);
      };
      media.onerror = () => {
        if (mounted.current)
          setError("Recording failed. Try the microphone again.");
        stop();
      };
      media.onstop = async () => {
        stream.current?.getTracks().forEach((track) => track.stop());
        clearInterval(timer.current);
        const blob = new Blob(chunks, { type: mimeType });
        if (blob.size)
          try {
            await completed(blob);
          } catch (reason) {
            if (mounted.current) setError(message(reason));
          }
        if (mounted.current) {
          setRecording(false);
          setBusy(false);
        }
      };
      media.start();
      setRecording(true);
      setSeconds(0);
      const started = performance.now();
      timer.current = setInterval(() => {
        const elapsed = Math.floor((performance.now() - started) / 1000);
        if (mounted.current) setSeconds(elapsed);
        // Leave room for browser stop scheduling and codec padding inside the server's 120-second limit.
        if (elapsed >= 119) stop();
      }, 250);
    } catch (reason) {
      stream.current?.getTracks().forEach((track) => track.stop());
      setError(message(reason));
    } finally {
      if (mounted.current) setBusy(false);
    }
  };
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      stop();
    };
  }, []);
  return { recording, seconds, error, busy, start, stop };
}
