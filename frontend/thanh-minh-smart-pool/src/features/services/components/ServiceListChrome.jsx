import { SearchOutlined } from '@ant-design/icons'
import { Input } from 'antd'

const categories = [
  { key: 'all', label: 'Tất cả' },
  { key: 'Sale', label: 'Bán hàng' },
  { key: 'Rental', label: 'Cho thuê' },
]

export function ServiceListHeader({ title, tabs, action }) {
  return <header className="service-list-header"><h1>{title}</h1>{tabs}<div className="service-list-primary-action">{action}</div></header>
}

export function ServiceNameCell({ name, type }) {
  const isSale = type === 'Sale'
  return (
    <div className="service-name-cell">
      <div className="service-name-heading"><span className={`service-type-prefix ${isSale ? 'service-type-sale' : 'service-type-rental'}`}>{isSale ? 'BÁN HÀNG' : 'CHO THUÊ'}</span><strong>{name}</strong></div>
    </div>
  )
}

export function ServiceCategoryTabs({ value, onChange }) {
  const handleKeyDown = (event) => {
    const index = categories.findIndex((category) => category.key === value)
    let nextIndex
    if (event.key === 'ArrowRight') nextIndex = (index + 1) % categories.length
    else if (event.key === 'ArrowLeft') nextIndex = (index + categories.length - 1) % categories.length
    else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = categories.length - 1
    else return
    event.preventDefault()
    onChange(categories[nextIndex].key)
    event.currentTarget.querySelectorAll('[role="tab"]')[nextIndex].focus()
  }
  return (
    <div className="service-category-tabs" role="tablist" aria-label="Loại dịch vụ" onKeyDown={handleKeyDown}>
      {categories.map((category) => {
        const active = value === category.key
        return <button className="service-category-tab" key={category.key} type="button" role="tab" tabIndex={active ? 0 : -1} aria-selected={active} onClick={() => onChange(category.key)}>{category.label}</button>
      })}
    </div>
  )
}

export function ServiceListToolbar({ children, searchTerm, onSearch, disabled = false, searchLabel = 'Tìm dịch vụ' }) {
  return (
    <section className="service-list-toolbar" aria-label="Bộ lọc danh sách">
      <div className="service-list-filters">{children}</div>
      <label className="service-search-control"><span>{searchLabel}</span><Input allowClear aria-label={searchLabel} placeholder="Nhập tên dịch vụ" prefix={<SearchOutlined />} value={searchTerm} disabled={disabled} onChange={(event) => onSearch(event.target.value)} /></label>
    </section>
  )
}
