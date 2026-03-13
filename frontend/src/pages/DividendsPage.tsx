import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { Info } from 'lucide-react';
import { formatPln, formatUsd, formatRate, formatDate } from '../format';
import type { AppState } from '../types';

export default function DividendsPage({ state }: { state: AppState }) {
  const navigate = useNavigate();

  if (!state.sessionId || !state.summary) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-slate-400">
        <p className="mb-4">No data imported yet.</p>
        <button onClick={() => navigate('/')} className="bg-blue-600 hover:bg-blue-500 text-white font-medium px-4 py-2 rounded-lg transition-colors">
          Import Statement
        </button>
      </div>
    );
  }

  const { dividends } = state;

  const totals = useMemo(() => ({
    amountPln: dividends.reduce((s, d) => s + d.amountPln, 0),
    withholdingPln: dividends.reduce((s, d) => s + d.withholdingTaxPln, 0),
    plTax: dividends.reduce((s, d) => s + d.amountPln * 0.19, 0),
    netOwed: dividends.reduce((s, d) => s + Math.max(d.amountPln * 0.19 - d.withholdingTaxPln, 0), 0),
  }), [dividends]);

  return (
    <div className="space-y-6">
      <h1 className="text-white text-2xl font-bold">Dividends</h1>

      <div className="flex items-start gap-3 bg-blue-500/10 border border-blue-500/30 rounded-xl p-4">
        <Info className="w-5 h-5 text-blue-400 shrink-0 mt-0.5" />
        <div className="text-sm text-blue-300">
          <p>Polish tax on dividends is 19%. US withholding tax (typically 15% under the PL-US treaty) can be credited against the Polish tax.</p>
          <p className="mt-1">Net tax owed in Poland = 19% - 15% = <strong>4%</strong> (when US tax rate is exactly 15%).</p>
        </div>
      </div>

      <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-700">
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-left">Date</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-left">Symbol</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Amount (USD)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Rate</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Amount (PLN)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">US Tax (PLN)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">PL Tax 19%</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Net Tax Owed</th>
              </tr>
            </thead>
            <tbody>
              {dividends.map((d, i) => {
                const plTax = d.amountPln * 0.19;
                const netOwed = Math.max(plTax - d.withholdingTaxPln, 0);
                return (
                  <tr
                    key={i}
                    className={`border-b border-slate-700/50 hover:bg-slate-700/30 transition-colors ${d.rateUnavailable ? 'bg-amber-500/5' : ''}`}
                  >
                    <td className="px-3 py-2.5 text-slate-300 whitespace-nowrap">{formatDate(d.date)}</td>
                    <td className="px-3 py-2.5 text-white font-medium">{d.symbol}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-300">{formatUsd(d.amountOriginal)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-400">{formatRate(d.exchangeRate)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-blue-400">{formatPln(d.amountPln)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-red-400">{formatPln(d.withholdingTaxPln)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-300">{formatPln(plTax)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-amber-400">{formatPln(netOwed)}</td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr className="border-t border-slate-600 bg-slate-800/80">
                <td colSpan={4} className="px-3 py-3 text-slate-300 font-medium text-sm">Totals</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-blue-400 text-sm">{formatPln(totals.amountPln)}</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-red-400 text-sm">{formatPln(totals.withholdingPln)}</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-slate-300 text-sm">{formatPln(totals.plTax)}</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-amber-400 text-sm">{formatPln(totals.netOwed)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>
    </div>
  );
}
