import { useState, useCallback } from 'react';
import { Upload, ChevronDown, ChevronUp, Loader2, CheckCircle2, AlertCircle, Gift } from 'lucide-react';
import { importFiles } from '../api';
import type { GiftedLotOverride } from '../api';
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

type GiftedFormState = {
  symbol: string;
  date: string;
  price: string;
  currency: string;
};

function parseSymbolFromError(msg: string): string {
  const m = msg.match(/Cannot sell [\d.]+ shares of (\S+)/);
  return m ? m[1] : '';
}

export default function ImportPage({ onComplete }: { onComplete: (data: AppState) => void }) {
  const [step, setStep] = useState<Step>('idle');
  const [error, setError] = useState('');
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [giftedForm, setGiftedForm] = useState<GiftedFormState | null>(null);

  const [dragOver, setDragOver] = useState(false);

  const processFiles = useCallback(async (files: File[], assumeGiftedShares = false, giftedLotOverride?: GiftedLotOverride) => {
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
      const result = await importFiles(allFiles, assumeGiftedShares, giftedLotOverride);
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
      setGiftedForm(null);
      setStep('error');
      setError(err instanceof Error ? err.message : 'Import failed');
    }
  }, [onComplete, pendingFiles]);

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

          {error && (() => {
            const isMissingLotsError = error.includes('Cannot sell');
            return (
              <div className="mt-4 space-y-3">
                <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-4 text-red-400 text-sm space-y-2">
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
                  {isMissingLotsError && pendingFiles.length > 0 && !giftedForm && (
                    <div className="ml-7 pt-2 border-t border-red-500/20">
                      <button
                        onClick={() => setGiftedForm({
                          symbol: parseSymbolFromError(error),
                          date: '',
                          price: '',
                          currency: 'USD',
                        })}
                        className="flex items-center gap-2 px-4 py-2 rounded-lg bg-amber-500/20 border border-amber-500/40 text-amber-300 hover:bg-amber-500/30 transition-colors text-sm font-medium"
                      >
                        <Gift className="w-4 h-4" />
                        These shares were gifted — enter grant details
                      </button>
                    </div>
                  )}
                </div>

                {giftedForm && (
                  <div className="bg-slate-800 border border-amber-500/30 rounded-lg p-5 space-y-4">
                    <div>
                      <p className="text-slate-200 text-sm font-medium mb-1">Gifted shares — grant details</p>
                      <p className="text-slate-400 text-xs">
                        Under Art.&nbsp;22 ust.&nbsp;1d updof, the cost basis equals the <strong className="text-slate-300">market value on the grant date</strong> — the same value you should have declared as income (przychód z innych źródeł) in the year of receipt.
                        Using PLN&nbsp;0 instead may result in paying more capital gains tax than legally required.
                      </p>
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="block text-xs text-slate-400 mb-1">Symbol</label>
                        <input
                          readOnly
                          value={giftedForm.symbol}
                          className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-1.5 text-sm text-slate-300 cursor-not-allowed"
                        />
                      </div>
                      <div>
                        <label className="block text-xs text-slate-400 mb-1">Currency</label>
                        <input
                          value={giftedForm.currency}
                          onChange={e => setGiftedForm(f => f && ({ ...f, currency: e.target.value.toUpperCase() }))}
                          maxLength={3}
                          className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-1.5 text-sm text-white focus:outline-none focus:border-blue-500"
                        />
                      </div>
                      <div>
                        <label className="block text-xs text-slate-400 mb-1">Grant date</label>
                        <input
                          type="date"
                          value={giftedForm.date}
                          onChange={e => setGiftedForm(f => f && ({ ...f, date: e.target.value }))}
                          className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-1.5 text-sm text-white focus:outline-none focus:border-blue-500"
                        />
                      </div>
                      <div>
                        <label className="block text-xs text-slate-400 mb-1">Price per share ({giftedForm.currency})</label>
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          placeholder="e.g. 79.84"
                          value={giftedForm.price}
                          onChange={e => setGiftedForm(f => f && ({ ...f, price: e.target.value }))}
                          className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-1.5 text-sm text-white focus:outline-none focus:border-blue-500"
                        />
                      </div>
                    </div>

                    <div className="flex items-center gap-3 pt-1">
                      <button
                        disabled={!giftedForm.date || !giftedForm.price || Number(giftedForm.price) <= 0}
                        onClick={() => processFiles([], true, {
                          symbol: giftedForm.symbol,
                          date: giftedForm.date,
                          price: Number(giftedForm.price),
                          currency: giftedForm.currency,
                        })}
                        className="px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-500 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                      >
                        Proceed with entered values
                      </button>
                      <button
                        onClick={() => processFiles([], true)}
                        className="px-4 py-2 rounded-lg bg-slate-700 border border-slate-600 text-slate-400 text-sm hover:text-white transition-colors"
                        title="PLN 0 cost is not tax-correct — it may result in paying more capital gains tax than legally required"
                      >
                        I don't know the price — use PLN&nbsp;0 (may overpay tax)
                      </button>
                    </div>
                  </div>
                )}
              </div>
            );
          })()}

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
