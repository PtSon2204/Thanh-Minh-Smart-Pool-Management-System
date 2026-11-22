import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import { transformWithOxc } from 'vite'
import { normalizeCameraSearch, validateCameraSearch } from '../../../features/safety/utils/cameraSearch.js'

// Render the page with controlled hooks and query data. Simulate React's keyed
// child replacement so URL navigation cannot silently discard submitted filters.
async function fixture(initialParams = '') {
  const cameras = [
    { id: '1', name: 'Camera demo cảnh báo', location: 'cao hơn mét 7 phía bên phải' },
    { id: '2', name: 'Camera demo cảnh báo 2', location: 'thấp hơn mét 7' },
    { id: '3', name: 'Camera trẻ em', location: 'Bể nhỏ' },
  ]
  let params = new URLSearchParams(initialParams), navigation = 0
  let slots = [], cursor = 0, effects = [], dirty = false, tree, previousKey
  const requests = []
  const element = (type, props, ...children) => ({ type, key: props?.key, props: { ...props, children } })
  const context = {
    React: { createElement: element, Fragment: 'Fragment' },
    normalizeCameraSearch, validateCameraSearch,
    useLocation: () => ({ key: String(navigation) }),
    useSearchParams: () => [params, next => { params = new URLSearchParams(next); navigation++; dirty = true }],
    useState: initial => {
      const index = cursor++
      if (!(index in slots)) slots[index] = typeof initial === 'function' ? initial() : initial
      return [slots[index], next => { slots[index] = typeof next === 'function' ? next(slots[index]) : next; dirty = true }]
    },
    useEffect: (effect, deps) => {
      const index = cursor++, old = slots[index]
      if (!old || deps.some((value, i) => value !== old[i])) effects.push(effect)
      slots[index] = deps
    },
    useMonitoringEvents: () => 'connected',
    useQuery: () => ({ data: [] }),
    useCameraList: filters => {
      requests.push({ ...filters })
      const term = normalizeCameraSearch(filters.search || '').toLowerCase()
      const items = cameras.filter(camera => (!filters.cameraId || camera.id === filters.cameraId)
        && [camera.name, camera.location].some(value => normalizeCameraSearch(value).toLowerCase().includes(term)))
      return { data: { items, totalCount: items.length } }
    },
    document: { getElementById: () => ({ scrollIntoView() {} }) },
    monitoringService: {}, startSignalRConnection() {},
    Typography: { Title: 'Title', Text: 'Text' }, Input: { Search: 'Search' },
    EnlargedCameraModal: 'EnlargedCameraModal',
    ...Object.fromEntries(['Alert', 'Button', 'Card', 'Empty', 'Pagination', 'Select', 'Space', 'Spin', 'Tag'].map(name => [name, name])),
  }
  const source = readFileSync(new URL('./CameraListPage.jsx', import.meta.url), 'utf8')
    .replace(/^import[^\n]*\r?\n/gm, '').replace('export default function ', 'function ')
  const compiled = await transformWithOxc(source, 'CameraListPage.jsx', { jsx: { runtime: 'classic' } })
  vm.createContext(context)
  vm.runInContext(compiled.code, context)
  function render() {
    for (let iteration = 0; iteration < 10; iteration++) {
      dirty = false
      const child = context.CameraListPage()
      if (child.key !== previousKey) slots = []
      previousKey = child.key
      cursor = 0; effects = []
      tree = child.type(child.props)
      effects.forEach(effect => effect())
      if (!dirty) return
    }
    assert.fail('Page did not settle')
  }
  function nodes(node) {
    if (arguments.length === 0) node = tree
    if (!node || typeof node !== 'object') return []
    if (Array.isArray(node)) return node.flatMap(item => nodes(item))
    return [node, ...nodes(node.props?.children ?? null)]
  }
  render()
  return {
    requests,
    nodes,
    get params() { return params },
    type(value) {
      nodes().find(node => node.type === 'Search').props.onChange({ target: { value } })
      render()
    },
    search(value) {
      nodes().find(node => node.type === 'Search').props.onChange({ target: { value } })
      render()
      nodes().find(node => node.type === 'Search').props.onSearch(value)
      render()
    },
    navigate(query) { params = new URLSearchParams(query); navigation++; render() },
    reset() {
      nodes().find(node => node.type === 'Button' && node.props.children.includes('Đặt lại')).props.onClick()
      render()
    },
  }
}

test('typing a camera search does not apply a filter before submitting', async () => {
  const page = await fixture()
  page.type('xyzabc')
  assert.equal(page.requests.at(-1).search, undefined)
  assert.equal(page.nodes().filter(node => node.props?.camera).length, 3)
  page.search('xyzabc')
  assert.equal(page.requests.at(-1).search, 'xyzabc')
  assert.equal(page.nodes().filter(node => node.props?.camera).length, 0)
})

for (const value of ['', '   ', 't', '%_']) {
  test(`initial invalid submission ${JSON.stringify(value)} does not navigate or show all cameras`, async () => {
    const page = await fixture()
    const params = page.params
    page.search(value)
    assert.equal(page.params, params)
    assert.equal(page.nodes().filter(node => node.props?.camera).length, 0)
    assert.ok(page.nodes().some(node => node.props?.role === 'alert'))
  })
}

test('submitting an unmatched camera search survives URL navigation and renders no camera cards', async () => {
  const page = await fixture()
  page.search('xyzabc')
  assert.equal(page.requests.at(-1).search, 'xyzabc')
  assert.equal(page.nodes().filter(node => node.props?.camera).length, 0)
  assert.ok(page.nodes().some(node => node.type === 'Empty' && node.props.description === 'Không tìm thấy camera phù hợp'))
  assert.equal(page.nodes().find(node => node.type === 'Search').props.value, 'xyzabc')
})

test('search clears a popup camera selection without losing its term and still accepts later popup navigation', async () => {
  const page = await fixture('cameraId=1&severity=CRITICAL')
  page.search('bể nhỏ')
  assert.equal(page.params.has('cameraId'), false)
  assert.equal(page.requests.at(-1).search, 'bểnhỏ')
  assert.deepEqual(page.nodes().filter(node => node.props?.camera).map(node => node.props.camera.id), ['3'])
  page.navigate('cameraId=2&severity=WARNING')
  assert.equal(page.requests.at(-1).cameraId, '2')
  assert.equal(page.requests.at(-1).search, undefined)
  assert.equal(page.nodes().find(node => node.type === 'Search').props.value, '')
})

for (const value of ['', ' \t\n ']) {
  test(`empty submission ${JSON.stringify(value)} shows validation without clearing the applied search`, async () => {
    const page = await fixture()
    page.search('bể nhỏ')
    const navigation = page.params
    const requestStart = page.requests.length
    page.search(value)
    assert.equal(page.params, navigation, 'Invalid submission must not navigate')
    assert.ok(page.requests.slice(requestStart).every(filters => filters.search === 'bểnhỏ'),
      'Invalid submission must not issue an unfiltered query')
    assert.equal(page.nodes().filter(node => node.props?.camera).length, 0)
    assert.ok(page.nodes().some(node => node.props?.role === 'alert'
      && node.props.children.includes('Vui lòng nhập tên hoặc vị trí camera trước khi tìm kiếm.')))
    page.reset()
    assert.equal(page.requests.at(-1).search, undefined)
    assert.equal(page.nodes().filter(node => node.props?.camera).length, 3)
    assert.equal(page.nodes().some(node => node.props?.role === 'alert'), false)
  })
}
