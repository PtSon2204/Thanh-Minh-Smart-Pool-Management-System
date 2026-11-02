import { Alert, Button, DatePicker, Descriptions, Form, Input, Select, Space, Steps, Typography } from 'antd'
import { useEffect, useRef, useState } from 'react'
import dayjs from 'dayjs'
import { documentLabels, documentStatusLabels, invitationRoleOptions, phonePattern, profileFromForm, vietnamToday } from '../onboarding/onboardingUi'

const required = (message) => ({ required: true, whitespace: true, message })
const steps = [
  { title: 'Thông tin cá nhân', fields: ['fullName', 'phone', 'dateOfBirth', 'cccdNumber', 'permanentAddress', 'temporaryAddress'] },
  { title: 'Ngân hàng và thuế', fields: ['bankName', 'bankAccountNumber', 'taxIdentifierKind', 'taxIdentifier'] },
  { title: 'Liên hệ và ảnh', fields: ['emergencyContactName', 'emergencyContactPhone', 'emergencyContactRelationship'] },
  { title: 'Kiểm tra và gửi', fields: ['password', 'passwordConfirmation'] },
]

function ProfileReview({ values, documents = [] }) {
  const shown = (value) => value?.trim() || 'Chưa nhập'
  return <div className="onboarding-review">
    <Descriptions title="Thông tin cá nhân" bordered column={{ xs: 1, sm: 2 }} size="small">
      <Descriptions.Item label="Họ và tên">{shown(values.fullName)}</Descriptions.Item>
      <Descriptions.Item label="Điện thoại">{shown(values.phone)}</Descriptions.Item>
      <Descriptions.Item label="Ngày sinh">{values.dateOfBirth?.format('DD/MM/YYYY') || 'Chưa nhập'}</Descriptions.Item>
      <Descriptions.Item label="Số CCCD">{shown(values.cccdNumber)}</Descriptions.Item>
      <Descriptions.Item label="Thường trú" span={2}>{shown(values.permanentAddress)}</Descriptions.Item>
      <Descriptions.Item label="Tạm trú" span={2}>{shown(values.temporaryAddress)}</Descriptions.Item>
    </Descriptions>
    <Descriptions title="Ngân hàng và thuế" bordered column={{ xs: 1, sm: 2 }} size="small">
      <Descriptions.Item label="Ngân hàng">{shown(values.bankName)}</Descriptions.Item>
      <Descriptions.Item label="Số tài khoản">{shown(values.bankAccountNumber)}</Descriptions.Item>
      <Descriptions.Item label="Loại định danh thuế">{values.taxIdentifierKind === 'LEGACY_TAX_CODE' ? 'Mã số thuế cũ (kiểm tra thủ công)' : values.taxIdentifierKind === 'PERSONAL_ID' ? 'Số định danh cá nhân (CCCD)' : 'Chưa chọn'}</Descriptions.Item>
      <Descriptions.Item label="Định danh thuế">{shown(values.taxIdentifier)}</Descriptions.Item>
    </Descriptions>
    <Descriptions title="Liên hệ khẩn cấp và ảnh" bordered column={{ xs: 1, sm: 2 }} size="small">
      <Descriptions.Item label="Người liên hệ">{values.emergencyContactName?.trim() || 'Không cung cấp'}</Descriptions.Item>
      <Descriptions.Item label="Điện thoại liên hệ">{values.emergencyContactPhone?.trim() || 'Không cung cấp'}</Descriptions.Item>
      <Descriptions.Item label="Mối quan hệ" span={2}>{values.emergencyContactRelationship?.trim() || 'Không cung cấp'}</Descriptions.Item>
      {Object.entries(documentLabels).map(([purpose, label]) => {
        const matching = documents.filter((document) => document.purpose === purpose)
        const latest = matching.find((document) => document.status === 'AVAILABLE') || matching[matching.length - 1]
        return <Descriptions.Item key={purpose} label={label}>{latest ? documentStatusLabels[latest.status] || latest.status : 'Không cung cấp'}</Descriptions.Item>
      })}
    </Descriptions>
  </div>
}

export default function OnboardingProfileForm({ revision, invitation, disabled, busy, requirePassword, onSave, onSubmit, documents }) {
  const [form] = Form.useForm()
  const [initialValues] = useState(() => ({
    ...revision.profile,
    dateOfBirth: revision.profile.dateOfBirth ? dayjs(revision.profile.dateOfBirth) : null,
  }))
  const [currentStep, setCurrentStep] = useState(0)
  const [saveMessage, setSaveMessage] = useState('')
  const [validationMessage, setValidationMessage] = useState('')
  const stepHeading = useRef(null)
  const stepChanged = useRef(false)
  const invalidField = useRef(null)
  const taxKind = Form.useWatch('taxIdentifierKind', form)
  const contactName = Form.useWatch('emergencyContactName', form)
  const contactPhone = Form.useWatch('emergencyContactPhone', form)
  const contactRelationship = Form.useWatch('emergencyContactRelationship', form)
  const hasContact = Boolean(contactName?.trim() || contactPhone?.trim() || contactRelationship?.trim())
  const latestBirthYear = Number(vietnamToday().slice(0, 4)) - 18
  const locked = disabled || busy

  useEffect(() => {
    if (!stepChanged.current) return
    stepHeading.current?.focus()
    if (invalidField.current) {
      form.scrollToField(invalidField.current, { focus: true, block: 'center' })
      invalidField.current = null
    }
  }, [currentStep, form])

  const changeStep = (nextStep) => {
    stepChanged.current = true
    setValidationMessage('')
    setCurrentStep(nextStep)
  }

  const showValidation = ({ errorFields }) => {
    const firstField = errorFields?.[0]?.name
    if (!firstField) return
    const invalidStep = steps.findIndex((step) => step.fields.includes(firstField[0]))
    setValidationMessage('Kiểm tra các trường được đánh dấu trước khi tiếp tục.')
    if (invalidStep >= 0 && invalidStep !== currentStep) {
      invalidField.current = firstField
      stepChanged.current = true
      setCurrentStep(invalidStep)
    } else {
      form.scrollToField(firstField, { focus: true, block: 'center' })
    }
  }

  const continueStep = async () => {
    if (locked) return
    try {
      await form.validateFields(steps[currentStep].fields)
      changeStep(currentStep + 1)
    } catch (failure) {
      showValidation(failure)
    }
  }

  const saveDraft = async () => {
    if (locked) return
    setSaveMessage('')
    const saved = await onSave(profileFromForm(form.getFieldsValue(true)))
    if (saved) setSaveMessage('Đã lưu bản nháp. Bạn có thể tiếp tục nhập hồ sơ.')
  }

  const submit = async (values) => {
    if (locked || currentStep !== steps.length - 1) return
    const saved = await onSubmit({ profile: profileFromForm(values), password: values.password, passwordConfirmation: values.passwordConfirmation })
    if (saved) form.setFieldsValue({ password: undefined, passwordConfirmation: undefined })
  }

  return <Form form={form} layout="vertical" className="onboarding-workspace" disabled={locked} autoComplete="off" initialValues={initialValues} preserve onFinish={submit} onFinishFailed={showValidation} onValuesChange={() => setSaveMessage('')} onSubmitCapture={(event) => {
    if (currentStep < steps.length - 1) {
      event.preventDefault()
      event.stopPropagation()
      continueStep()
    }
  }}>
    {invitation && <Alert type="info" showIcon title="Thông tin lời mời do quản trị viên quyết định" description={<div className="onboarding-invitation-meta"><span>{invitation.email}</span><span>{invitationRoleOptions.find((role) => role.value === invitation.targetRole)?.label || invitation.targetRole}</span><span>Ngày vào làm: {dayjs(invitation.joinDate).format('DD/MM/YYYY')}</span></div>} />}
    <nav className="onboarding-stepper" aria-label="Các bước hoàn thiện hồ sơ">
      <Steps current={currentStep} size="small" orientation="horizontal" responsive={false} titlePlacement="vertical" items={steps.map(({ title }) => ({ title }))} />
    </nav>
    <div className="onboarding-step-content">
      <header className="onboarding-step-heading" ref={stepHeading} tabIndex={-1}>
        <Typography.Text type="secondary">Bước {currentStep + 1} / {steps.length}</Typography.Text>
        <Typography.Title level={3}>{steps[currentStep].title}</Typography.Title>
      </header>
      {validationMessage && <Alert type="error" showIcon title={validationMessage} />}
      <section hidden={currentStep !== 0} aria-label="Thông tin cá nhân">
        <div className="onboarding-form-grid">
          <Form.Item name="fullName" label="Họ và tên" rules={[required('Nhập họ và tên.'), { max: 255 }]}><Input maxLength={255} autoComplete="name" /></Form.Item>
          <Form.Item name="phone" label="Điện thoại" rules={[required('Nhập số điện thoại.'), { pattern: phonePattern, message: 'Nhập số di động Việt Nam, bắt đầu bằng 0 hoặc +84.' }]}><Input inputMode="tel" autoComplete="tel" /></Form.Item>
          <Form.Item name="dateOfBirth" label="Ngày sinh" extra={`Đủ 18 tuổi theo năm sinh: năm ${latestBirthYear} trở về trước, không xét ngày hoặc tháng.`} rules={[{ required: true, message: 'Chọn ngày sinh.' }, { validator: (_, value) => !value || (value.isValid() && value.year() <= Number(vietnamToday().slice(0, 4)) - 18) ? Promise.resolve() : Promise.reject(new Error('Nhân viên phải đủ 18 tuổi theo năm sinh tại Việt Nam.')) }]}><DatePicker format="DD/MM/YYYY" className="service-full-width" disabledDate={(date) => date.year() > Number(vietnamToday().slice(0, 4)) - 18} /></Form.Item>
          <Form.Item name="cccdNumber" label="Số CCCD" rules={[required('Nhập số CCCD.'), { pattern: /^[0-9]{12}$/, message: 'CCCD gồm đúng 12 chữ số.' }]}><Input inputMode="numeric" maxLength={12} /></Form.Item>
          <Form.Item name="permanentAddress" label="Địa chỉ thường trú" rules={[required('Nhập địa chỉ thường trú.'), { max: 500 }]}><Input.TextArea rows={2} maxLength={500} /></Form.Item>
          <div className="onboarding-address-field"><Form.Item name="temporaryAddress" label="Địa chỉ tạm trú" rules={[required('Nhập địa chỉ tạm trú.'), { max: 500 }]}><Input.TextArea rows={2} maxLength={500} /></Form.Item><Button htmlType="button" onClick={() => { form.setFieldValue('temporaryAddress', form.getFieldValue('permanentAddress')); setSaveMessage('') }}>Giống địa chỉ thường trú</Button></div>
        </div>
      </section>
      <section hidden={currentStep !== 1} aria-label="Ngân hàng và thuế">
        <div className="onboarding-form-grid">
          <Form.Item name="bankName" label="Tên ngân hàng" rules={[required('Nhập tên ngân hàng.'), { max: 150 }]}><Input maxLength={150} /></Form.Item>
          <Form.Item name="bankAccountNumber" label="Số tài khoản ngân hàng" rules={[required('Nhập số tài khoản.'), { pattern: /^[0-9]{6,30}$/, message: 'Số tài khoản gồm 6–30 chữ số.' }]}><Input inputMode="numeric" maxLength={30} /></Form.Item>
          <Form.Item name="taxIdentifierKind" label="Loại định danh thuế" rules={[{ required: true, message: 'Chọn loại định danh thuế.' }]}><Select options={[{ value: 'PERSONAL_ID', label: 'Số định danh cá nhân (CCCD)' }, { value: 'LEGACY_TAX_CODE', label: 'Mã số thuế cũ (cần kiểm tra thủ công)' }]} /></Form.Item>
          <Form.Item name="taxIdentifier" label={taxKind === 'LEGACY_TAX_CODE' ? 'Mã số thuế cũ' : 'Số định danh dùng cho thuế'} dependencies={['cccdNumber', 'taxIdentifierKind']} rules={[required('Nhập định danh thuế.'), ({ getFieldValue }) => ({ validator(_, value) { const kind = getFieldValue('taxIdentifierKind'); if (!value) return Promise.resolve(); if (kind === 'LEGACY_TAX_CODE') return /^[0-9]{10}$/.test(value) ? Promise.resolve() : Promise.reject(new Error('Mã số thuế cũ gồm 10 chữ số.')); return /^[0-9]{12}$/.test(value) && value === getFieldValue('cccdNumber') ? Promise.resolve() : Promise.reject(new Error('Số định danh thuế phải trùng số CCCD 12 chữ số.')) } })]}><Input inputMode="numeric" maxLength={12} /></Form.Item>
        </div>
        <Typography.Paragraph type="secondary" className="onboarding-field-note">Từ 01/07/2025, số định danh cá nhân thay mã số thuế cá nhân. Mã số thuế cũ cần kiểm tra thủ công. Hệ thống không xác minh với cơ quan thuế hoặc ngân hàng.</Typography.Paragraph>
      </section>
      <section hidden={currentStep !== 2} aria-label="Liên hệ và ảnh tùy chọn">
        <div className="onboarding-form-grid">
          <Form.Item name="emergencyContactName" label="Tên người liên hệ" rules={[{ required: hasContact, whitespace: true, message: 'Nhập tên người liên hệ.' }, { max: 255 }]}><Input maxLength={255} /></Form.Item>
          <Form.Item name="emergencyContactPhone" label="Điện thoại người liên hệ" rules={[{ required: hasContact, message: 'Nhập điện thoại người liên hệ.' }, { pattern: phonePattern, message: 'Nhập số di động Việt Nam hợp lệ.' }]}><Input inputMode="tel" /></Form.Item>
          <Form.Item name="emergencyContactRelationship" label="Mối quan hệ" rules={[{ max: 100 }]}><Input maxLength={100} /></Form.Item>
        </div>
        <div className="onboarding-optional-documents"><Typography.Title level={4}>Ảnh tùy chọn</Typography.Title>{documents}</div>
      </section>
      <section hidden={currentStep !== 3} aria-label="Kiểm tra và gửi hồ sơ">
        <Form.Item noStyle shouldUpdate>{() => <ProfileReview values={form.getFieldsValue(true)} documents={revision.documents} />}</Form.Item>
        {requirePassword && <div className="onboarding-password-section"><Typography.Title level={4}>Mật khẩu đăng nhập sau khi được duyệt</Typography.Title><Typography.Paragraph type="secondary">Chưa có quyền nhân viên trước khi được duyệt. Hệ thống không tự đăng nhập hoặc gửi mật khẩu qua email.</Typography.Paragraph><div className="onboarding-form-grid">
          <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Nhập mật khẩu.' }, { min: 8, message: 'Mật khẩu cần ít nhất 8 ký tự.' }, { validator: (_, value) => !value || (/[A-Za-zÀ-ỹ]/.test(value) && /[0-9]/.test(value) && new TextEncoder().encode(value).length <= 72) ? Promise.resolve() : Promise.reject(new Error('Cần chữ và số, tối đa 72 byte UTF-8.')) }]}><Input.Password autoComplete="new-password" /></Form.Item>
          <Form.Item name="passwordConfirmation" label="Xác nhận mật khẩu" dependencies={['password']} rules={[{ required: true, message: 'Xác nhận mật khẩu.' }, ({ getFieldValue }) => ({ validator: (_, value) => !value || value === getFieldValue('password') ? Promise.resolve() : Promise.reject(new Error('Mật khẩu xác nhận không khớp.')) })]}><Input.Password autoComplete="new-password" /></Form.Item>
        </div></div>}
      </section>
    </div>
    <footer className="onboarding-step-actions">
      <div className="onboarding-save-state"><Button htmlType="button" disabled={locked} onClick={saveDraft}>Lưu bản nháp</Button><Typography.Text type="secondary" role="status">{saveMessage || 'Bản nháp chỉ được lưu khi bạn chọn Lưu bản nháp.'}</Typography.Text></div>
      <Space wrap>
        {currentStep > 0 && <Button htmlType="button" disabled={locked} onClick={() => changeStep(currentStep - 1)}>Quay lại</Button>}
        {currentStep < steps.length - 1
          ? <Button key="continue" type="primary" htmlType="button" disabled={locked} onClick={continueStep}>Tiếp tục</Button>
          : <Button key="submit" type="primary" htmlType="submit" disabled={disabled} loading={busy}>Gửi hồ sơ để duyệt</Button>}
      </Space>
    </footer>
  </Form>
}
