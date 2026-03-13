import { useState, useCallback } from 'react';
import { Upload, ChevronDown, ChevronUp, Loader2, CheckCircle2, AlertCircle } from 'lucide-react';
import { importFile } from '../api';
import type { AppState } from '../types';

type Step = 'idle' | 'parsing' | 'rates' | 'calculating' | 'done' | 'error';

const steps: { key: Step; label: string }[] = [
  { key: 'parsing', label: 'Parsing file...' },
  { key: 'rates', label: 'Fetching PLN exchange rates...' },
  { key: 'calculating', label: 'Calculating taxes...' },
  { key: 'done', label: 'Complete!' },
];

export default function ImportPage({ onComplete }: { onComplete: (data: AppState) => void }) {
  const [step, setStep] = useState<Step>('idle');
  const [error, setError] = useState('');
  const [helpOpen, setHelpOpen] = useState(false);
  const [dragOver, setDragOver] = useState(false);

  const processFile = useCallback(async (file: File) => {
    if (!file.name.endsWith('.csv')) {
      setError('Please upload a CSV file.');
      return;
    }
    setError('');
    setStep('parsing');

    const timer1 = setTimeout(() => setStep('rates'), 500);
    const timer2 = setTimeout(() => setStep('calculating'), 1200);

    try {
      const result = await importFile(file);
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
    const file = e.dataTransfer.files[0];
    if (file) processFile(file);
  }, [processFile]);

  const handleFileInput = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) processFile(file);
  }, [processFile]);

  const isProcessing = step !== 'idle' && step !== 'error';

  return (
    <div className="max-w-2xl mx-auto mt-16">
      <h1 className="text-white text-3xl font-bold text-center mb-2">Import Activity Statement</h1>
      <p className="text-slate-400 text-center mb-10">
        Upload your Interactive Brokers Activity Statement to calculate Polish taxes
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
            <p className="text-slate-300 font-medium mb-1">Drag & drop your CSV file here</p>
            <p className="text-slate-500 text-sm">or click to browse</p>
            <input type="file" accept=".csv" className="hidden" onChange={handleFileInput} />
          </label>

          {error && (
            <div className="mt-4 flex items-center gap-2 bg-red-500/10 border border-red-500/30 rounded-lg p-4 text-red-400 text-sm">
              <AlertCircle className="w-5 h-5 shrink-0" />
              {error}
            </div>
          )}

          <div className="mt-8 bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
            <button
              onClick={() => setHelpOpen(!helpOpen)}
              className="w-full flex items-center justify-between px-6 py-4 text-left text-slate-300 hover:text-white transition-colors"
            >
              <span className="font-medium">How to export from Interactive Brokers</span>
              {helpOpen ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
            </button>
            {helpOpen && (
              <div className="px-6 pb-5 text-slate-400 text-sm space-y-2">
                <p>1. Log in to IB Client Portal</p>
                <p>2. Go to <strong className="text-slate-300">Reports &rarr; Statements &rarr; Activity</strong></p>
                <p>3. Select period (full year), format: <strong className="text-slate-300">CSV</strong></p>
                <p>4. Download and upload here</p>
              </div>
            )}
          </div>
        </>
      ) : (
        <div className="bg-slate-800 border border-slate-700 rounded-xl p-8">
          <div className="space-y-4">
            {steps.map(({ key, label }) => {
              const idx = steps.findIndex(s => s.key === key);
              const currentIdx = steps.findIndex(s => s.key === step);
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
            })}
          </div>
        </div>
      )}
    </div>
  );
}
