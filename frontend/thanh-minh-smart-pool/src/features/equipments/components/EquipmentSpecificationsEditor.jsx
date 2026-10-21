import { Button, Input, Select, Tooltip } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { newSpecification } from '../utils/equipmentSpecifications'

const emptySpecifications = { type: 'object', children: [] }

function SpecificationValueInput({ node, onChange, label, disabled }) {
  if (node.type !== 'value') return <SpecificationFields node={node} onChange={onChange} disabled={disabled} path={label} />
  if (typeof node.value === 'boolean') {
    return <Select aria-label={label} value={node.value} disabled={disabled} onChange={value => onChange({ ...node, value })}
      options={[{ label: 'Có', value: true }, { label: 'Không', value: false }]} />
  }
  return <Input.TextArea aria-label={label} autoSize={{ minRows: 1, maxRows: 5 }} disabled={disabled}
    value={node.value == null ? '' : String(node.value)} placeholder="Ví dụ: 2 kW, 380 V..."
    onChange={event => {
      const text = event.target.value
      const value = typeof node.value === 'number' && text.trim() && Number.isFinite(Number(text)) ? Number(text) : text
      onChange({ ...node, value })
    }} />
}

function SpecificationFields({ node, onChange, disabled, path = '' }) {
  const array = node.type === 'array'

  function updateChild(index, changes) {
    onChange({ ...node, children: node.children.map((child, position) => position === index ? { ...child, ...changes } : child) })
  }

  return <div className={`equipment-specification-fields ${path ? 'equipment-specification-fields--nested' : ''}`}>
    {node.children.length > 0 && <div className="equipment-specification-labels" aria-hidden="true"><span>{array ? 'Mục' : 'Tên thông số'}</span><span>Giá trị</span><span /></div>}
    {node.children.map((child, index) => <div className="equipment-specification-row" key={child.id}>
      {array ? <span className="equipment-specification-item-label">Mục {index + 1}</span> :
        <Input aria-label={`${path ? `${path} / ` : ''}Tên thông số ${index + 1}`} value={child.name} disabled={disabled} placeholder="Ví dụ: Công suất"
          onChange={event => updateChild(index, { name: event.target.value })} />}
      <div className="equipment-specification-value"><SpecificationValueInput node={child.node} disabled={disabled}
        label={`${path ? `${path} / ` : ''}Giá trị ${child.name || index + 1}`} onChange={value => updateChild(index, { node: value })} /></div>
      <Tooltip title={array ? 'Xóa mục' : 'Xóa thông số'}><Button type="text" danger icon={<DeleteOutlined />} disabled={disabled}
        aria-label={`Xóa ${child.name || `mục ${index + 1}`}`} onClick={() => onChange({ ...node, children: node.children.filter((_, position) => position !== index) })} /></Tooltip>
    </div>)}
    {!node.children.length && <p className="equipment-specifications-empty">Chưa có thông số. Bạn có thể thêm các đặc tính riêng của thiết bị.</p>}
    <Button type="dashed" className="equipment-add-specification" icon={<PlusOutlined />} disabled={disabled}
      onClick={() => onChange({ ...node, children: [...node.children, newSpecification()] })}>{array ? 'Thêm giá trị' : 'Thêm thông số'}</Button>
  </div>
}

export default function EquipmentSpecificationsEditor({ value = emptySpecifications, onChange, disabled, id }) {
  return <div id={id} className="equipment-specifications-editor">
    {value.type === 'value' ? <>
      <div className="equipment-specification-legacy"><span>Thông số chung</span><SpecificationValueInput node={value} onChange={onChange} label="Giá trị thông số chung" disabled={disabled} />
        <Tooltip title="Xóa thông số"><Button type="text" danger icon={<DeleteOutlined />} aria-label="Xóa thông số chung" disabled={disabled} onClick={() => onChange({ type: 'object', children: [] })} /></Tooltip>
      </div>
      <Button type="dashed" className="equipment-add-specification" icon={<PlusOutlined />} disabled={disabled}
        onClick={() => onChange({ type: 'object', children: [{ id: crypto.randomUUID(), name: 'Thông số chung', node: value }, newSpecification()] })}>Thêm thông số</Button>
    </> : <SpecificationFields node={value} onChange={onChange} disabled={disabled} />}
  </div>
}
