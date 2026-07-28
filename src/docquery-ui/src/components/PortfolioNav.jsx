import { useState } from 'react';

const PORTFOLIO = 'https://vondraysanford.com';

// Mirrors the landing page's navbar (manually — keep in sync if the
// portfolio's sections change). Every link exits to the portfolio; the
// "/ docquery" suffix on the logo marks which room of the site you're in.
const NAV_LINKS = [
  ['About', 'about'],
  ['Skills', 'skills'],
  ['Experience', 'experience'],
  ['Certs', 'certifications'],
  ['Projects', 'projects'],
  ['Lab', 'lab'],
  ['Writing', 'writing'],
  ['Contact', 'contact'],
];

export default function PortfolioNav() {
  const [open, setOpen] = useState(false);

  return (
    <nav className="portfolio-nav">
      <div className="nav-inner">
        <a href={PORTFOLIO} className="logo">
          vondray<span>.sanford</span>
          <span className="logo-here"> / docquery</span>
        </a>
        <ul className={`nav-links ${open ? 'open' : ''}`}>
          {NAV_LINKS.map(([label, anchor]) => (
            <li key={anchor}>
              <a href={`${PORTFOLIO}/#${anchor}`} onClick={() => setOpen(false)}>
                {label}
              </a>
            </li>
          ))}
        </ul>
        <button
          type="button"
          className="hamburger"
          aria-label="Toggle navigation"
          aria-expanded={open}
          onClick={() => setOpen((wasOpen) => !wasOpen)}
        >
          <span></span>
          <span></span>
          <span></span>
        </button>
      </div>
    </nav>
  );
}
