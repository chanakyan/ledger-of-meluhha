_cite_registry = {}

function _register_cite(key, full, short)
  _cite_registry[key] = {full=full, short=short, seen=false}
end

function _smartcite(key)
  local r = _cite_registry[key]
  if r == nil then
    tex.print("\\footnote{[UNKNOWN CITATION KEY: " .. key .. "]}")
    return
  end
  if r.seen then
    tex.print("\\footnote{" .. r.short .. ", \\textit{op.~cit.}}")
  else
    tex.print("\\footnote{" .. r.full .. "}")
    r.seen = true
  end
end

_register_cite([[Mahadevan1977]],
  [[Iravatham Mahadevan. \textit{The Indus Script: Texts, Concordance and Tables}. Archaeological Survey of India, New Delhi, 1977.]],
  [[Mahadevan (1977)]])

_register_cite([[FSW2004]],
  [[Steve Farmer, Richard Sproat, and Michael Witzel. The Collapse of the Indus-Script Thesis: The Myth of a Literate Harappan Civilization. \textit{Electronic Journal of Vedic Studies} 11/2 (2004): 19--57. \url{https://safarmer.com/fsw2.pdf}]],
  [[Farmer, Sproat \& Witzel (2004)]])

_register_cite([[Rao1973]],
  [[S.R. Rao. \textit{Lothal and the Indus Civilisation}. Asia Publishing House, Bombay, 1973.]],
  [[Rao (1973)]])

_register_cite([[Hemmy1931]],
  [[A.S. Hemmy. System of Weights at Mohenjo-daro. In J. Marshall (ed.), \textit{Mohenjo-daro and the Indus Civilisation}, vol. II, 589--598. Arthur Probsthain, London, 1931.]],
  [[Hemmy (1931)]])

_register_cite([[Kenoyer2010]],
  [[J.M. Kenoyer. Measuring the Harappan World. In I. Morley and C. Renfrew (eds.), \textit{The Archaeology of Measurement}. Cambridge University Press, 2010.]],
  [[Kenoyer (2010)]])

_register_cite([[Brahmagupta628]],
  [[Brahmagupta. \textit{Brahmasphutasiddhanta} (628 CE). Discussed in G.G. Joseph, \textit{The Crest of the Peacock: Non-European Roots of Mathematics}, 3rd ed. Princeton University Press, 2011.]],
  [[Brahmagupta (628 CE) via Joseph (2011)]])

_register_cite([[Rao2009]],
  [[Rajesh P.N. Rao, Nisha Yadav, Mayank N. Vahia, et al. A Markov Model of the Indus Script. \textit{PNAS} 106/33 (2009): 13296--13301. \url{https://doi.org/10.1073/pnas.0906237106}]],
  [[Rao et al. (2009)]])

_register_cite([[Rao2010]],
  [[Rajesh P.N. Rao, Nisha Yadav, Mayank N. Vahia, et al. Statistical Analysis of the Indus Script Using n-Grams. \textit{PLOS ONE} (March 2010). \url{https://doi.org/10.1371/journal.pone.0009506}]],
  [[Rao et al. (2010)]])

_register_cite([[Ratnagar2004]],
  [[Shereen Ratnagar. \textit{Trading Encounters: From the Euphrates to the Indus in the Bronze Age}. Oxford University Press, New Delhi, 2004.]],
  [[Ratnagar (2004)]])

_register_cite([[Ratnagar2003]],
  [[Shereen Ratnagar. Theorizing Bronze-Age Intercultural Trade: The Evidence of the Weights. \textit{Pal\'{e}orient} 29/1 (2003). \url{https://doi.org/10.3406/paleo.2003.4760}]],
  [[Ratnagar (2003)]])

_register_cite([[Kalyanaraman2018]],
  [[S. Kalyanaraman. \textit{Indus Script as Kara\d{n}am: Wealth-Accounting Ledgers of the Sarasvat\={\i} Civilisation}. Sarasvati Research Centre, Chennai, 2018. Also available via \url{https://www.academia.edu/73471181}]],
  [[Kalyanaraman (2018)]])

_register_cite([[IndoMeso2026]],
  [[Wikipedia. Indo-Mesopotamia relations (revised March 2026). \url{https://en.wikipedia.org/wiki/Indo-Mesopotamia_relations}]],
  [[Wikipedia, Indo-Mesopotamia relations (2026)]])

_register_cite([[WikiIndus]],
  [[Wikipedia. Indus script (revised April 2026). \url{https://en.wikipedia.org/wiki/Indus_script}]],
  [[Wikipedia, Indus script (2026)]])

_register_cite([[RajanSivanantham2025]],
  [[K. Rajan and R. Sivanantham. \textit{Indus Signs and Graffiti Marks of Tamil Nadu: A Morphological Study}. T.N.D.A. pub. no. 357. Department of Archaeology, Government of Tamil Nadu, Chennai, 2025. ISBN 9788197784255.]],
  [[Rajan \& Sivanantham (2025)]])

_register_cite([[MayigCISI]],
  [[May (mayig). \textit{Indus Valley Script Corpus}. GitHub repository, 2024. Digitisation of Joshi \& Parpola CISI corpus (Mohenjo-daro M-1 through M-199). \url{https://github.com/mayig/indus-valley-script-corpus}. CC BY 4.0.]],
  [[May/mayig (2024)]])

_register_cite([[WellsFuls2006]],
  [[Bryan K. Wells and Andreas Fuls. \textit{Interactive Corpus of Indus Texts} (ICIT). Digital database, 2006--2022. 4,660 artefacts, 5,644 texts, 17,957 signs. Wells sign list v2.8 (709 signs). Access: \texttt{andreas.fuls@tu-berlin.de}. \url{https://www.indus.epigraphica.de/}]],
  [[Wells \& Fuls (2006)]])

_register_cite([[JoshiParpola1987]],
  [[Jagat Pati Joshi and Asko Parpola (eds.). \textit{Corpus of Indus Seals and Inscriptions}. Vol. 1: Collections in India. Suomalainen Tiedeakatemia, Helsinki, 1987.]],
  [[Joshi \& Parpola (1987)]])

_register_cite([[Parpola1994]],
  [[Asko Parpola. \textit{Deciphering the Indus Script}. Cambridge University Press, Cambridge, 1994.]],
  [[Parpola (1994)]])

_register_cite([[Parpola2008]],
  [[Asko Parpola. Is the Indus script indeed not a writing system? In \textit{Air\={a}vati: Felicitation volume in honour of Iravatham Mahadevan}, pp.\ 111--131. varalaaru.com, Chennai, 2008. \url{https://www.harappa.com/sites/default/files/pdf/indus-writing.pdf}]],
  [[Parpola (2008)]])

_register_cite([[Fuls2022]],
  [[Andreas Fuls. \textit{Corpus of Indus Inscriptions: Volume~1 --- Inscriptions, Sign List, and Concordances}. de Gruyter, Berlin, 2022. ISBN 978-3-11-073998-2. 571~pp. Print monograph of the Interactive Corpus of Indus Texts (ICIT), Wells sign list v2.8: 709~signs, 4{,}660~artefacts, 5{,}644~texts, 19{,}831~sign occurrences. Accompanied by Volume~2: \textit{A Catalogue of Indus Signs} (per-sign morphology, allograph notes, and Mahadevan/Parpola/Wells concordance).]],
  [[Fuls (2022, Vol.~1)]])
