// The Mizan mark: a balance scale (the meaning of "ميزان") next to the name.
// Drawn as inline SVG so it takes the theme colours and needs no image file.
export default function Logo({ size = 28, light = true }) {
  return (
    <span className="d-inline-flex align-items-center gap-2">
      <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
        <g stroke="var(--mizan-gold)" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round">
          <path d="M16 6v20M10 26h12M7 10h18" />
          <path d="M7 10l-3 7h6zM25 10l-3 7h6z" />
        </g>
      </svg>
      <span className={`fw-bold ${light ? 'text-white' : 'page-title'}`}>Mizan</span>
      {/* lang/dir: screen readers pronounce it as Arabic */}
      <span className="brand-ar" lang="ar" dir="rtl">ميزان</span>
    </span>
  )
}
