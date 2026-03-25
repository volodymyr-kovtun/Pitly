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

    setError('');
    setStep('parsing');

    const timer1 = setTimeout(() => setStep('rates'), 500);
    const timer2 = setTimeout(() => setStep('calculating'), 1200);

    try {
      const result = await importFiles(files);
      clearTimeout(timer1);
      clearTimeout(timer2);
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
      setStep('error');
      setError(err instanceof Error ? err.message : 'Import failed');
    }
  }, [onComplete]);

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
            <div className="mt-4 flex items-center gap-2 bg-red-500/10 border border-red-500/30 rounded-lg p-4 text-red-400 text-sm">
              <AlertCircle className="w-5 h-5 shrink-0" />
              {error}
            </div>
          )}

          <div className="mt-8 space-y-2">
            <HelpSection title="How to export from Interactive Brokers">
              <p>1. Log in to IB Client Portal</p>
              <p>2. Go to <strong className="text-slate-300">Reports &rarr; Statements &rarr; Activity</strong></p>
              <p>3. Select period (full year), format: <strong className="text-slate-300">CSV</strong></p>
              <p>4. If you sold shares bought in earlier years, upload those earlier yearly CSVs together.</p>
              <p>5. If the statement includes stock splits, keep the related earlier years in the same upload.</p>
            </HelpSection>
            <HelpSection title="How to export from Trading 212">
              <p>1. Log in to Trading 212</p>
              <p>2. Go to <strong className="text-slate-300">History</strong> (clock icon)</p>
              <p>3. Click the <strong className="text-slate-300">Download</strong> icon</p>
              <p>4. Select date range (full tax year) and export as CSV</p>
              <p>5. Upload one or more yearly CSVs together if you need prior-year FIFO history</p>
            </HelpSection>
            <HelpSection title="How to export from Exante">
              <p>1. Log in to your Exante Client Area</p>
              <p>2. Go to <strong className="text-slate-300">Reports &rarr; Transactions</strong></p>
              <p>3. Select date range and click <strong className="text-slate-300">Export selected &rarr; CSV</strong></p>
              <p>4. Upload the generated CSV (or TSV) file</p>
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
