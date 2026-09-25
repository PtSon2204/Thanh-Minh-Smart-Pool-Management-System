import './FloatingContact.css'

// ✏️ Thay link của bạn tại đây:
const MESSENGER_LINK = 'https://m.me/YOUR_PAGE_ID'   // ← dán link Messenger vào đây
const ZALO_LINK      = 'https://zalo.me/YOUR_PHONE'  // ← dán link Zalo vào đây

export default function FloatingContact() {
  return (
    <div className="floating-contacts">
      {/* Messenger */}
      <a
        href={MESSENGER_LINK}
        target="_blank"
        rel="noreferrer"
        className="floating-btn messenger"
        data-tooltip="Nhắn tin Messenger"
        aria-label="Messenger"
      >
        {/* Messenger icon SVG */}
        <svg viewBox="0 0 36 36" width="26" height="26" fill="white" xmlns="http://www.w3.org/2000/svg">
          <path d="M18 2C9.163 2 2 8.775 2 17.1c0 4.596 1.968 8.713 5.13 11.598V34l4.7-2.58A16.54 16.54 0 0018 32.2c8.837 0 16-6.775 16-15.1S26.837 2 18 2zm1.59 20.34-4.07-4.34-7.95 4.34 8.74-9.28 4.17 4.34 7.85-4.34-8.74 9.28z"/>
        </svg>
      </a>

      {/* Zalo */}
      <a
        href={ZALO_LINK}
        target="_blank"
        rel="noreferrer"
        className="floating-btn zalo"
        data-tooltip="Chat Zalo"
        aria-label="Zalo"
      >
        {/* Zalo icon SVG */}
        <svg viewBox="0 0 50 50" width="28" height="28" fill="white" xmlns="http://www.w3.org/2000/svg">
          <path d="M25 3C12.85 3 3 12.85 3 25s9.85 22 22 22 22-9.85 22-22S37.15 3 25 3zm-6.5 30H12v-1.5l7.2-9.8H12v-2h6.5v1.5l-7.1 9.8H18.5V33zm6-1c0 .6-.4 1-1 1h-1v-2h1c.6 0 1 .4 1 1zm1-10c-2.2 0-4 1.8-4 4v6h-2V22h2v1.1c.8-.7 1.9-1.1 3-1.1 2.8 0 5 2.2 5 5v6h-2v-6c0-1.7-1.3-3-3-3zm11.5 11h-7v-1.5l4.7-8.8H32v-2h6.5V22l-4.6 8.8H38.5V33z"/>
        </svg>
      </a>
    </div>
  )
}
