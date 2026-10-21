import { parseEquipmentMetadata, specificationValueText } from '../utils/equipmentSpecifications'

function SpecificationValue({ value }) {
  if (value !== null && typeof value === 'object') {
    const entries = Object.entries(value)
    if (!entries.length) return <span className="equipment-muted">Chưa có thông số</span>
    const array = Array.isArray(value)
    return <dl className="equipment-specification-details">{entries.map(([name, item], index) => <div key={name}>
      <dt>{array ? `Mục ${index + 1}` : name}</dt><dd><SpecificationValue value={item} /></dd>
    </div>)}</dl>
  }
  return <span className="equipment-specification-text">{specificationValueText(value)}</span>
}

export default function EquipmentSpecificationsDetails({ metadata }) {
  const value = parseEquipmentMetadata(metadata)
  return value == null ? <p className="equipment-muted">Chưa có thông số thiết bị.</p> : <SpecificationValue value={value} />
}
