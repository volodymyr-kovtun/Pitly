import { useMemo, useState } from 'react';
import { Printer, FileText, ChevronDown, ChevronUp } from 'lucide-react';
import { formatPln, formatTaxPeriod, hasCustomTaxPeriod } from '../format';
import type { AppState, Pit38Fields, TaxSummary } from '../types';
import EmptyState from '../components/EmptyState';

const TAX_RATE = 0.19;

function roundToFullPln(value: number): number {
  return Math.round(value);
}

// Art. 63 § 1a Ordynacji podatkowej — lump-sum tax under art. 30a ust. 1 pkt 1-3
// is rounded to full groszy upward (footnote 5 on PIT-38(17) form).
function roundToGroszUp(value: number): number {
  return Math.ceil(value * 100) / 100;
}

function buildPit38(s: TaxSummary): Pit38Fields {
  const przychody = s.totalProceedsPln;
  const koszty = s.totalCostPln;
  const difference = przychody - koszty;
  const dochod = Math.max(difference, 0);
  const strata = Math.max(-difference, 0);

  const podstawa = roundToFullPln(dochod);
  const podatek = Math.round(podstawa * TAX_RATE * 100) / 100;
  const podatekNalezny = roundToFullPln(podatek);

  const zryczaltowanyPodatek = roundToGroszUp(s.totalDividendsPln * TAX_RATE);
  const podatekZaGranica = Math.round(Math.min(s.totalWithholdingPln, zryczaltowanyPodatek) * 100) / 100;
  const roznica = roundToGroszUp(Math.max(zryczaltowanyPodatek - podatekZaGranica, 0));
  const podatekDoZaplaty = podatekNalezny + roznica;

  return {
    year: s.year,
    poz22Przychody: przychody,
    poz23Koszty: koszty,
    poz24RazemPrzychody: przychody,
    poz25RazemKoszty: koszty,
    poz26Dochod: Math.round(dochod * 100) / 100,
    poz27Strata: Math.round(strata * 100) / 100,
    poz29PodstawaObliczenia: podstawa,
    poz31Podatek: podatek,
    poz33PodatekNalezny: podatekNalezny,
    poz45ZryczaltowanyPodatek: zryczaltowanyPodatek,
    poz46PodatekZaplaconyZaGranica: podatekZaGranica,
    poz47Roznica: roznica,
    poz49PodatekDoZaplaty: podatekDoZaplaty,
    totalDividendsPln: s.totalDividendsPln,
  };
}

export default function Pit38Page({ state }: { state: AppState }) {
  const pit38 = useMemo(() => state.summary ? buildPit38(state.summary) : null, [state.summary]);

  if (!state.sessionId || !state.summary || !pit38) {
    return <EmptyState />;
  }
  const year = pit38.year;
  const customTaxPeriod = hasCustomTaxPeriod(state.summary.year, state.summary.taxableFrom);

  return (
    <div className="space-y-8 max-w-4xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-white text-2xl font-bold">Your PIT-38 Declaration for {year}</h1>
          <p className="text-slate-400 mt-1">
            Field numbers (poz.) match the official PIT-38(17) form.
          </p>
          <p className="text-slate-500 text-sm mt-1">
            Taxable period: {formatTaxPeriod(state.summary.taxableFrom, state.summary.taxableTo)}
          </p>
        </div>
        <button
          onClick={() => window.print()}
          className="no-print bg-blue-600 hover:bg-blue-500 text-white font-medium px-4 py-2 rounded-lg transition-colors flex items-center gap-2"
        >
          <Printer className="w-4 h-4" />
          Export as PDF
        </button>
      </div>

      {customTaxPeriod && (
        <div className="bg-amber-500/10 border border-amber-500/30 rounded-xl p-4 text-sm text-amber-200">
          Earlier uploaded history is still used to reconstruct FIFO costs and stock splits, but this PIT-38 summary
          includes only dividends and sells from the taxable period above.
        </div>
      )}

      <Section title="C. Dochody / Straty — art. 30b ust. 1 ustawy">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-700">
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-left">Source</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-right">Przychod (b)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-right">Koszty (c)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-right">Dochod (d)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-right">Strata (e)</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-b border-slate-700/50">
                <td className="px-6 py-3 text-slate-500">
                  <span className="text-slate-600 font-mono text-xs mr-2">poz. 20, 21</span>
                  PIT-8C
                </td>
                <td className="px-6 py-3 text-right font-mono text-slate-600">&mdash;</td>
                <td className="px-6 py-3 text-right font-mono text-slate-600">&mdash;</td>
                <td className="px-6 py-3"></td>
                <td className="px-6 py-3"></td>
              </tr>
              <tr className="border-b border-slate-700/50 bg-slate-700/20">
                <td className="px-6 py-3 text-slate-300">
                  <span className="text-blue-400 font-mono text-xs mr-2">poz. 22, 23</span>
                  Inne przychody
                </td>
                <td className="px-6 py-3 text-right font-mono text-white font-medium">{formatPln(pit38.poz22Przychody)}</td>
                <td className="px-6 py-3 text-right font-mono text-white font-medium">{formatPln(pit38.poz23Koszty)}</td>
                <td className="px-6 py-3"></td>
                <td className="px-6 py-3"></td>
              </tr>
              <tr className="border-b border-slate-700/50 bg-slate-700/30">
                <td className="px-6 py-3 text-slate-300 font-semibold">
                  <span className="text-blue-400 font-mono text-xs mr-2">poz. 24–27</span>
                  Razem
                </td>
                <td className="px-6 py-3 text-right font-mono text-white font-medium">{formatPln(pit38.poz24RazemPrzychody)}</td>
                <td className="px-6 py-3 text-right font-mono text-white font-medium">{formatPln(pit38.poz25RazemKoszty)}</td>
                <td className="px-6 py-3 text-right font-mono text-white font-medium">
                  {pit38.poz26Dochod > 0 ? formatPln(pit38.poz26Dochod) : <span className="text-slate-600">&mdash;</span>}
                </td>
                <td className="px-6 py-3 text-right font-mono text-white font-medium">
                  {pit38.poz27Strata > 0 ? formatPln(pit38.poz27Strata) : <span className="text-slate-600">&mdash;</span>}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div className="px-6 py-2 text-slate-500 text-xs">
          Foreign brokers do not issue PIT-8C — all income goes to row 2 &quot;Inne przychody&quot; (poz. 22-23).
        </div>
      </Section>

      <Section title="D. Obliczenie zobowiazania podatkowego — art. 30b ust. 1 ustawy">
        <FieldTable rows={[
          { poz: '29', name: 'Podstawa obliczenia podatku', value: pit38.poz29PodstawaObliczenia, note: 'Poz. 26 minus straty z lat ubieglych, zaokraglone do pelnych zl' },
          { poz: '31', name: 'Podatek (poz. 29 \u00d7 19%)', value: pit38.poz31Podatek },
          { poz: '33', name: 'Podatek nalezny', value: pit38.poz33PodatekNalezny, note: 'Po zaokragleniu do pelnych zl' },
        ]} />
      </Section>

      <Section title="G. Podatek do zaplaty — dywidendy zagraniczne (art. 30a)">
        <div className="px-6 py-3 border-b border-slate-700/50">
          <span className="text-slate-500 text-xs">Gross dividends received: </span>
          <span className="font-mono text-slate-400 text-sm">{formatPln(pit38.totalDividendsPln)}</span>
          <span className="text-slate-600 text-xs ml-2">(informational — not entered on PIT-38)</span>
        </div>
        <FieldTable rows={[
          { poz: '45', name: 'Zryczaltowany podatek 19% od dywidend zagranicznych', value: pit38.poz45ZryczaltowanyPodatek },
          { poz: '46', name: 'Podatek zaplacony za granica (US withholding)', value: pit38.poz46PodatekZaplaconyZaGranica, note: 'Nie moze przekroczyc kwoty z poz. 45' },
          { poz: '47', name: 'Roznica (poz. 45 \u2212 poz. 46)', value: pit38.poz47Roznica, note: 'Zaokraglone do pelnych groszy w gore (art. 63 § 1a O.p.)' },
        ]} />
      </Section>

      <div className="bg-blue-500/10 border-2 border-blue-500 rounded-xl p-8 text-center">
        <FileText className="w-10 h-10 text-blue-400 mx-auto mb-3" />
        <p className="text-slate-500 text-xs font-mono mb-1">Poz. 49</p>
        <p className="text-blue-300 text-sm mb-1">PODATEK DO ZAPLATY</p>
        <p className="text-white text-3xl font-bold font-mono tabular-nums">{formatPln(pit38.poz49PodatekDoZaplaty)}</p>
        <p className="text-slate-400 text-xs mt-1">= poz. 33 + poz. 47</p>
        <p className="text-slate-400 text-sm mt-2">Deadline: April 30, {year + 1}</p>
      </div>

      <EpityGuide pit38={pit38} year={year} hasDividends={pit38.totalDividendsPln > 0} />
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

function FieldTable({ rows }: { rows: { poz: string; name: string; value: number; note?: string }[] }) {
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b border-slate-700">
          <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-left">Poz.</th>
          <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-left">Field Name</th>
          <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-6 py-3 text-right">Value</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(({ poz, name, value, note }) => (
          <tr key={poz} className="border-b border-slate-700/50 hover:bg-slate-700/30 transition-colors">
            <td className="px-6 py-3 text-blue-400 font-mono font-medium">{poz}</td>
            <td className="px-6 py-3">
              <span className="text-slate-300">{name}</span>
              {note && <span className="block text-slate-500 text-xs mt-0.5">{note}</span>}
            </td>
            <td className="px-6 py-3 text-right font-mono tabular-nums text-white font-medium">
              {formatPln(value)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Val({ v }: { v: number }) {
  return <span className="font-mono text-blue-400 font-medium">{formatPln(v)}</span>;
}

function EpityGuide({ pit38, year, hasDividends }: { pit38: Pit38Fields; year: number; hasDividends: boolean }) {
  const [open, setOpen] = useState(false);
  const hasCapitalGains = pit38.poz22Przychody > 0 || pit38.poz23Koszty > 0;

  return (
    <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
      <button
        onClick={() => setOpen(!open)}
        className="w-full px-6 py-4 flex items-center justify-between text-left hover:bg-slate-700/30 transition-colors"
      >
        <h2 className="text-white text-lg font-semibold">
          Step-by-step guide: File PIT-38 via e-pity Kreator
        </h2>
        {open
          ? <ChevronUp className="w-5 h-5 text-slate-400 shrink-0" />
          : <ChevronDown className="w-5 h-5 text-slate-400 shrink-0" />}
      </button>

      {open && (
        <div className="px-6 pb-6 space-y-6 border-t border-slate-700">
          <p className="text-slate-400 text-sm pt-4">
            This guide walks you through the <strong className="text-slate-300">e-pity.pl Kreator PIT</strong> web
            version, step by step. Your calculated values are shown in <span className="text-blue-400">blue</span>.
          </p>

          {/* Step 1 */}
          <GuideStep n={1} title="Sposob opodatkowania">
            <p>
              Go to <span className="text-blue-400">e-pity.pl</span> &rarr; <strong className="text-white">Kreator
              PIT</strong> (in the left sidebar). You can also go directly to the Kreator
              at <span className="text-blue-400">e-pity.pl/pit-online/kreator-{year}/</span>.
            </p>
            <ul className="list-disc list-inside space-y-1 mt-2">
              <li>Sposob opodatkowania: select <strong className="text-white">indywidualnie</strong></li>
              <li>Cel zlozenia deklaracji: select <strong className="text-white">zlozenie zeznania</strong></li>
              <li>You can optionally check &quot;Pobierz Twoj e-PIT&quot; to import pre-filled data from podatki.gov.pl</li>
            </ul>
            <p>Click <strong className="text-white">DALEJ</strong>.</p>
          </GuideStep>

          {/* Step 2 */}
          <GuideStep n={2} title="Dane podatnika">
            <p>
              Enter or verify your personal data: PESEL, full name, date of birth, and residential
              address as of December 31, {year}. If you used e-pity before, this may be pre-filled.
            </p>
            <p>Click <strong className="text-white">DALEJ</strong>.</p>
          </GuideStep>

          {/* Step 3 */}
          <GuideStep n={3} title="Przychody podatnika — add income source">
            <p>
              On the &quot;Dodaj przychody podatnika&quot; screen, click
              the <strong className="text-white">+ POZOSTALE</strong> button.
            </p>
            <p>
              From the dropdown, under &quot;Pozostale przychody&quot;, select:<br />
              <strong className="text-white">Przychody kapitalowe ze sprzedazy udzialow (PIT-38)</strong>
            </p>
            <p className="text-slate-500 text-xs">
              Do NOT click &quot;PIT-8C&quot; — foreign brokers do not issue PIT-8C.
            </p>
          </GuideStep>

          {/* Step 4 */}
          <GuideStep n={4} title="Przychody podatnika — enter capital gains values">
            <p>
              After selecting PIT-38 income, the Kreator shows two fields.
              These map to PIT-38 poz. 22-23 (&quot;Inne przychody&quot; row):
            </p>
            <ul className="list-disc list-inside space-y-2 mt-2">
              <li>
                <strong className="text-white">&quot;Wpisz przychody z czesci E podlegajace opodatkowaniu&quot;</strong>
                {' '}&rarr; enter <Val v={pit38.poz22Przychody} />
                <span className="block text-slate-500 text-xs ml-6">
                  This is your total proceeds from selling securities in PLN (poz. 22).
                  {!hasCapitalGains && ' Enter 0 if you had no sell trades this year.'}
                </span>
              </li>
              <li>
                <strong className="text-white">&quot;Wpisz koszty zwiazane z przychodami z czesci E&quot;</strong>
                {' '}&rarr; enter <Val v={pit38.poz23Koszty} />
                <span className="block text-slate-500 text-xs ml-6">
                  This is your total acquisition cost (FIFO) plus commissions in PLN (poz. 23).
                  {!hasCapitalGains && ' Enter 0 if you had no sell trades this year.'}
                </span>
              </li>
            </ul>
            {!hasCapitalGains && (
              <div className="bg-slate-700/30 rounded-lg p-3 mt-2">
                <p className="text-slate-400 text-xs">
                  You had no sell trades in {year}, so both fields are 0. You still need to file
                  PIT-38 to report your foreign dividends.
                </p>
              </div>
            )}
            <p className="mt-2">Click <strong className="text-white">DALEJ</strong>.</p>
          </GuideStep>

          {/* Step 5 */}
          <GuideStep n={5} title={'PIT-38 — obliczenie zobowiazania podatkowego'}>
            <p>This is the main tax calculation screen. Fill in the following fields from top to bottom:</p>

            <div className="space-y-4 mt-3">
              <div>
                <p className="text-white font-medium text-xs uppercase tracking-wider mb-1">Straty z lat ubieglych</p>
                <p>
                  If you have unused losses from the past 5 years (max 50% per year), enter the amount.
                  Otherwise leave empty.
                </p>
              </div>

              <div>
                <p className="text-white font-medium text-xs uppercase tracking-wider mb-1">Zryczaltowany podatek dochodowy</p>
                <p>Leave empty (this is for other flat-rate taxes not withheld by a payer, poz. 44).</p>
              </div>

              <div>
                <p className="text-white font-medium text-xs uppercase tracking-wider mb-1">
                  Zryczaltowany podatek od przychodow uzyskanych poza granicami RP
                </p>
                {hasDividends ? (
                  <p>
                    Enter <Val v={pit38.poz45ZryczaltowanyPodatek} /> (poz. 45)
                    <span className="block text-slate-500 text-xs">
                      This is 19% of your total gross dividends in PLN ({formatPln(pit38.totalDividendsPln)})
                    </span>
                  </p>
                ) : (
                  <p>Leave empty — no foreign dividends to report.</p>
                )}
              </div>

              <div>
                <p className="text-white font-medium text-xs uppercase tracking-wider mb-1">
                  Kwoty podatku zaplaconego za granica (przeliczone na zlote)
                </p>
                {hasDividends ? (
                  <p>
                    Enter <Val v={pit38.poz46PodatekZaplaconyZaGranica} /> (poz. 46)
                    <span className="block text-slate-500 text-xs">
                      US withholding tax already deducted by your broker, converted to PLN. Cannot exceed poz. 45.
                    </span>
                  </p>
                ) : (
                  <p>Leave empty.</p>
                )}
              </div>

              <div>
                <p className="text-white font-medium text-xs uppercase tracking-wider mb-1">
                  Suma zaliczek / Dochody zwolnione z IPO / Monthly breakdown
                </p>
                <p>Leave all remaining fields empty (unless you have other specific circumstances).</p>
              </div>
            </div>
            <p className="mt-2">Click <strong className="text-white">DALEJ</strong>.</p>
          </GuideStep>

          {/* Step 6 */}
          <GuideStep n={6} title="Przekaz 1,5% podatku (optional)">
            <p>
              You can donate 1.5% of your tax to a Public Benefit Organization (OPP). Enter
              the KRS number of your chosen organization. This does not increase your tax — it
              redirects part of what you already owe. Skip if not interested.
            </p>
            <p>Click <strong className="text-white">DALEJ</strong>.</p>
          </GuideStep>

          {/* Step 7 */}
          <GuideStep n={7} title="Podsumowanie — review and submit">
            <p>The summary screen shows your calculated tax:</p>
            <ul className="list-disc list-inside space-y-1 mt-2">
              <li>Kwota dochodu: {hasCapitalGains ? formatPln(pit38.poz26Dochod) : '0,00 zl'}</li>
              <li>Kwota do zaplaty: <Val v={pit38.poz49PodatekDoZaplaty} /></li>
            </ul>
            <p className="mt-3">From here you can:</p>
            <ul className="list-disc list-inside space-y-2 mt-1">
              <li>
                Click <strong className="text-white">&quot;ZOBACZ DEKLARACJE&quot;</strong> to
                preview the full PIT-38 form and verify all field numbers
              </li>
              <li>
                Click <strong className="text-white">&quot;WYSLIJ E-DEKLARACJE&quot;</strong> to
                submit electronically to the tax office
              </li>
              <li>
                After submission, save the <strong className="text-white">UPO</strong> (Urzedowe
                Poswiadczenie Odbioru) — your official receipt
              </li>
              {pit38.poz49PodatekDoZaplaty > 0 && (
                <li>
                  Click <strong className="text-white">&quot;ZAPLAC PODATEK ONLINE&quot;</strong> or
                  &quot;DRUK PRZELEWU&quot; to pay
                </li>
              )}
            </ul>
            <p className="mt-2">
              Deadline: <strong className="text-white">April 30, {year + 1}</strong>.
              {pit38.poz49PodatekDoZaplaty > 0 && (
                <span> Tax of <Val v={pit38.poz49PodatekDoZaplaty} /> must be paid by the same date.</span>
              )}
            </p>
          </GuideStep>

          <div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-4 text-sm">
            <p className="text-amber-300 font-medium mb-1">Important notes</p>
            <ul className="list-disc list-inside space-y-1 text-slate-400">
              <li>PIT-38 is filed individually — joint filing with a spouse is not allowed for this form</li>
              <li>Even if you only have losses or only dividends, you <strong className="text-slate-300">must still file</strong> PIT-38</li>
              <li>Losses from capital gains can be deducted over the next 5 years (max 50% of each year&apos;s loss per year)</li>
              <li>All PLN amounts use NBP Table A mid rates from the last business day before each transaction</li>
              <li>e-pity automatically generates the PIT/ZG attachment for foreign income when you submit</li>
            </ul>
          </div>
        </div>
      )}
    </div>
  );
}

function GuideStep({ n, title, children }: { n: number; title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="text-white font-semibold flex items-center gap-3 mb-2">
        <span className="bg-blue-600 text-white text-xs font-bold w-6 h-6 rounded-full flex items-center justify-center shrink-0">
          {n}
        </span>
        {title}
      </h3>
      <div className="text-slate-300 text-sm space-y-2 ml-9">
        {children}
      </div>
    </div>
  );
}
