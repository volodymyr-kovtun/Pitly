import { useState, useCallback } from 'react';
import { Upload, ChevronDown, ChevronUp, Loader2, CheckCircle2, AlertCircle } from 'lucide-react';
import { importFiles } from '../api';
import type { AppState } from '../types';

type Step = 'idle' | 'parsing' | 'rates' | 'calculating' | 'done' | 'error';

const steps: { key: Step; label: string }[] = [
  { key: 'parsing', label: 'Parsing statements...' },
  { key: 'rates', label: 'Fetching PLN exchange rates...' },
  { key: 'calculating', label: 'Calculating taxes...' },
  { key: 'done', label: 'Complete!' },
];

function HelpSection({ title, children }: { title: string; children: React.ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-6 py-4 text-left text-slate-300 hover:text-white transition-colors"
      >
        <span className="font-medium">{title}</span>
        {open ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
      </button>
      {open && (
        <div className="px-6 pb-5 text-slate-400 text-sm space-y-2">
          {children}
        </div>
      )}
    </div>
  );
}

export default function ImportPage({ onComplete }: { onComplete: (data: AppState) => void }) {
  const [step, setStep] = useState<Step>('idle');
  const [error, setError] = useState('');
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [residencyStartDate, setResidencyStartDate] = useState('');

  const [dragOver, setDragOver] = useState(false);

  const processFiles = useCallback(async (files: File[]) => {
    if (files.length === 0) {
      setError('Please upload at least one CSV file.');
      return;
    }

    if (files.some(file => !file.name.toLowerCase().endsWith('.csv'))) {
      setError('Please upload CSV files only.');
      return;
    }

    // Merge with previously queued files (from a failed attempt), newer file wins on name collision
    const merged = new Map<string, File>();
    for (const f of pendingFiles) merged.set(f.name, f);
    for (const f of files) merged.set(f.name, f);
    const allFiles = Array.from(merged.values());

    setError('');
    setStep('parsing');

    const timer1 = setTimeout(() => setStep('rates'), 500);
    const timer2 = setTimeout(() => setStep('calculating'), 1200);

    try {
      const result = await importFiles(allFiles, residencyStartDate || undefined);
      clearTimeout(timer1);
      clearTimeout(timer2);
      setPendingFiles([]);
      setStep('done');
      setTimeout(() => {
        onComplete({
          sessionId: result.sessionId,
          summary: result.summary,
          trades: result.trades,
          dividends: result.dividends,
        });
      }, 600);
    } catch (err: unknown) {
      clearTimeout(timer1);
      clearTimeout(timer2);
      setPendingFiles(allFiles);
      setStep('error');
      setError(err instanceof Error ? err.message : 'Import failed');
    }
  }, [onComplete, pendingFiles, residencyStartDate]);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const files = Array.from(e.dataTransfer.files);
    if (files.length > 0) processFiles(files);
  }, [processFiles]);

  const handleFileInput = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    if (files.length > 0) processFiles(files);
  }, [processFiles]);

  const isProcessing = step !== 'idle' && step !== 'error';

  return (
    <div className="max-w-2xl mx-auto mt-16">
      <h1 className="text-white text-3xl font-bold text-center mb-2">Import Activity Statement</h1>
      <p className="text-slate-400 text-center mb-10">
        Upload one or more broker statements to calculate Polish taxes
      </p>

      {!isProcessing ? (
        <>
          <div className="mb-6 bg-slate-800 border border-slate-700 rounded-xl p-5">
            <label className="block">
              <span className="text-slate-200 font-medium">Polish tax residency start date</span>
              <span className="block text-slate-500 text-sm mt-1">
                Optional. Leave empty for a full-year calculation. If set, Pitly will report only dividends and sells
                on or after this date, while still using earlier uploaded history to reconstruct FIFO lots.
              </span>
              <input
                type="date"
                value={residencyStartDate}
                onChange={(e) => setResidencyStartDate(e.target.value)}
                className="mt-3 w-full md:w-auto bg-slate-900 border border-slate-700 rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-blue-500"
              />
            </label>
          </div>

          <label
            onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
            onDragLeave={() => setDragOver(false)}
            onDrop={handleDrop}
            className={`block border-2 border-dashed rounded-2xl p-16 text-center cursor-pointer transition-colors bg-slate-800/50 ${
              dragOver ? 'border-blue-500 bg-blue-500/10' : 'border-slate-600 hover:border-blue-500'
            }`}
          >
            <Upload className="w-12 h-12 text-slate-500 mx-auto mb-4" />
            <p className="text-slate-300 font-medium mb-1">Drag & drop your CSV files here</p>
            <p className="text-slate-500 text-sm">or click to browse. Upload prior years too if you sold older holdings.</p>
            <input type="file" accept=".csv" multiple className="hidden" onChange={handleFileInput} />
          </label>

          {error && (
            <div className="mt-4 bg-red-500/10 border border-red-500/30 rounded-lg p-4 text-red-400 text-sm space-y-2">
              <div className="flex items-start gap-2">
                <AlertCircle className="w-5 h-5 shrink-0 mt-0.5" />
                <span>{error}</span>
              </div>
              {pendingFiles.length > 0 && (
                <div className="ml-7 text-slate-400">
                  <span className="text-slate-300">Already queued:</span>{' '}
                  {pendingFiles.map(f => f.name).join(', ')}
                  <span className="block mt-1">Upload any additional prior-year files and they will be combined automatically.</span>
                </div>
              )}
            </div>
          )}

          <div className="mt-8 space-y-2">
            <HelpSection title="How to export from Interactive Brokers">
              <p>1. Log in to IB Client Portal</p>
              <p>2. Go to <strong className="text-slate-300">Reports &rarr; Statements &rarr; Activity</strong></p>
              <p>3. Select the full tax year, format: <strong className="text-slate-300">CSV</strong></p>
              <p>4. Even with a mid-year residency start date, still export the full calendar year so FIFO stays correct.</p>
              <p>5. If you sold shares bought in earlier years, upload those earlier yearly CSVs together.</p>
              <p>6. If the statement includes stock splits, keep the related earlier years in the same upload.</p>
            </HelpSection>
            <HelpSection title="How to export from Trading 212">
              <p>1. Log in to Trading 212</p>
              <p>2. Go to <strong className="text-slate-300">History</strong> (clock icon)</p>
              <p>3. Click the <strong className="text-slate-300">Download</strong> icon</p>
              <p>4. Select the full tax year and export as CSV</p>
              <p>5. Even with a mid-year residency start date, still export the full calendar year so FIFO stays correct.</p>
              <p>6. Upload one or more yearly CSVs together if you need prior-year FIFO history</p>
            </HelpSection>
          </div>
        </>
      ) : (
        <div className="bg-slate-800 border border-slate-700 rounded-xl p-8">
          <div className="space-y-4">
            {(() => {
              const currentIdx = steps.findIndex(s => s.key === step);
              return steps.map(({ key, label }, idx) => {
              const isDone = idx < currentIdx || step === 'done';
              const isCurrent = idx === currentIdx && step !== 'done';

              return (
                <div key={key} className="flex items-center gap-3">
                  {isDone ? (
                    <CheckCircle2 className="w-5 h-5 text-green-400" />
                  ) : isCurrent ? (
                    <Loader2 className="w-5 h-5 text-blue-400 animate-spin" />
                  ) : (
                    <div className="w-5 h-5 rounded-full border-2 border-slate-600" />
                  )}
                  <span className={isDone ? 'text-green-400' : isCurrent ? 'text-white' : 'text-slate-500'}>
                    {label}
                  </span>
                </div>
              );
            });
            })()}
          </div>
        </div>
      )}
    </div>
  );
}
