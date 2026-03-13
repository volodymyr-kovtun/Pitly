import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Printer, FileText } from 'lucide-react';
import { formatPln } from '../format';
import { getPit38 } from '../api';
import type { AppState, Pit38Fields } from '../types';

export default function Pit38Page({ state }: { state: AppState }) {
  const navigate = useNavigate();
  const [pit38, setPit38] = useState<Pit38Fields | null>(null);

  useEffect(() => {
    if (state.sessionId) {
      getPit38(state.sessionId).then(setPit38).catch(() => {});
    }
  }, [state.sessionId]);

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

  if (!pit38) {
    return <div className="text-slate-400 text-center mt-20">Loading PIT-38 data...</div>;
  }

  const year = pit38.year;

  return (
    <div className="space-y-8 max-w-4xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-white text-2xl font-bold">Your PIT-38 Declaration for {year}</h1>
          <p className="text-slate-400 mt-1">Below are the exact values to enter in each field of your PIT-38 form.</p>
        </div>
        <button
          onClick={() => window.print()}
          className="no-print bg-blue-600 hover:bg-blue-500 text-white font-medium px-4 py-2 rounded-lg transition-colors flex items-center gap-2"
        >
          <Printer className="w-4 h-4" />
          Export as PDF
        </button>
      </div>

      <Section title="Section C — Capital gains from securities">
        <FieldTable rows={[
          { field: 'C.20', name: 'Przychody (Proceeds)', value: pit38.c20_Przychody },
          { field: 'C.21', name: 'Koszty uzyskania przychodow (Costs)', value: pit38.c21_Koszty },
          { field: 'C.22', name: 'Dochod / Strata (Gain or Loss)', value: pit38.c22_DochodStrata },
          { field: 'C.23', name: 'Podstawa obliczenia podatku', value: pit38.c23_PodstawaObliczenia },
          { field: 'C.24', name: 'Podatek 19%', value: pit38.c24_Podatek19 },
        ]} />
      </Section>

      <Section title="Section D — Dividends and other income from foreign sources">
        <FieldTable rows={[
          { field: 'D.25', name: 'Przychody z dywidend', value: pit38.d25_PrzychodyDywidendy },
          { field: 'D.26', name: 'Zryczaltowany podatek 19%', value: pit38.d26_ZryczaltowanyPodatek19 },
        ]} />
      </Section>

      <Section title="Section E — Foreign tax credit">
        <FieldTable rows={[
          { field: 'E.27', name: 'Podatek zaplacony za granica (US withholding)', value: pit38.e27_PodatekZaplaconyZagranica },
          { field: 'E.28', name: 'Podatek do zaplaty po odliczeniu', value: pit38.e28_PodatekDoZaplaty },
        ]} />
      </Section>

      <div className="bg-blue-500/10 border-2 border-blue-500 rounded-xl p-8 text-center">
        <FileText className="w-10 h-10 text-blue-400 mx-auto mb-3" />
        <p className="text-blue-300 text-sm mb-1">Total tax to pay</p>
        <p className="text-white text-3xl font-bold font-mono tabular-nums">{formatPln(pit38.totalTaxOwed)}</p>
        <p className="text-slate-400 text-sm mt-2">Deadline: April 30, {year + 1}</p>
      </div>

      <div className="bg-slate-800 border border-slate-700 rounded-xl p-6 space-y-4">
        <h2 className="text-white text-lg font-semibold">How to file PIT-38 online via e-Urzad Skarbowy (e-US)</h2>
        <ol className="list-decimal list-inside space-y-2 text-slate-300 text-sm">
          <li>Go to <span className="text-blue-400">podatki.gov.pl</span></li>
          <li>Log in with Trusted Profile (Profil Zaufany) or banking identity</li>
          <li>Go to &quot;Twoj e-PIT&quot; or submit PIT-38 manually</li>
          <li>Enter the values from the table above into the corresponding fields</li>
          <li>Verify and submit before <strong className="text-white">April 30, {year + 1}</strong></li>
        </ol>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-700">
        <h2 className="text-white text-lg font-semibold">{title}</h2>
      </div>
      {children}
    </div>
  );
}

function FieldTable({ rows }: { rows: { field: string; name: string; value: number }[] }) {
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b border-slate-700">
          <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-left">PIT-38 Field</th>
          <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-left">Field Name (Polish)</th>
          <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-right">Your Value</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(({ field, name, value }) => (
          <tr key={field} className="border-b border-slate-700/50 hover:bg-slate-700/30 transition-colors">
            <td className="px-6 py-3 text-blue-400 font-mono font-medium">{field}</td>
            <td className="px-6 py-3 text-slate-300">{name}</td>
            <td className="px-6 py-3 text-right font-mono tabular-nums text-white font-medium">{formatPln(value)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
