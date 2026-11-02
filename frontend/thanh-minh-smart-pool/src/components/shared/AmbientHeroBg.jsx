export default function AmbientHeroBg() {
  return (
    <div className="ambient-hero-bg" aria-hidden="true">
      {/* Ambient large blur orbs */}
      <div className="ambient-orb orb-1" />
      <div className="ambient-orb orb-2" />
      <div className="ambient-orb orb-3" />
      <div className="ambient-orb orb-4" />

      {/* Floating glowing light spots / dots */}
      <div className="ambient-glow-dots">
        {[...Array(8)].map((_, i) => (
          <div key={i} className={`ambient-glow-dot agd-${i + 1}`} />
        ))}
      </div>

      {/* Subtle dot matrix pattern */}
      <div className="ambient-dots-pattern" />
    </div>
  )
}
