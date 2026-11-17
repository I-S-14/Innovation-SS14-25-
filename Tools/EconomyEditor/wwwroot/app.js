const $ = id => document.getElementById(id);
const clone = value => structuredClone(value);
const money = value => new Intl.NumberFormat('ru-RU').format(value);
const state = { catalog: null, project: null, vendor: '', tab: 0, page: 0, dirty: false, change: 0, saving: false };
let token = sessionStorage.getItem('economy-token') || '';
const itemById = new Map(), images = new Map(), metadata = new Map(), icons = new Map();
const LOCAL_KEY = 'is14-economy-draft-v1';
const PAGE_SIZE = 60;
let localTimer;

function element(tag, className, text) { const e = document.createElement(tag); if (className) e.className = className; if (text !== undefined) e.textContent = text; return e; }
function button(text, action, className = '') { const e = element('button', className, text); e.type = 'button'; e.addEventListener('click', action); return e; }
function notify(text, kind = '') { $('notice').textContent = text; $('notice').className = `notice ${kind}`; $('notice').hidden = false; }
function clearNotice() { $('notice').hidden = true; }
function modal(title) { $('modal-title').textContent = title; $('modal-content').replaceChildren(); if (!$('modal').open) $('modal').showModal(); return $('modal-content'); }
function field(parent, label, value, options = {}) {
  const l = element('label', 'form-field', label), input = element(options.textarea ? 'textarea' : 'input');
  input.value = value; if (options.type) input.type = options.type; input.maxLength = options.max || 150;
  l.append(input); parent.append(l); return input;
}
function currentVendor() { return state.project?.vendors.find(v => v.id === state.vendor); }
function currentEntries() { const v = currentVendor(); return !v ? [] : state.tab === -1 ? v.contrabandInventory : v.tabs[state.tab]?.inventory || []; }
function confirmReplace() { return !state.dirty || confirm('В проекте есть несохранённые изменения. Продолжить? Локальный черновик будет заменён.'); }
function ensureWritable() { if (state.project) return true; notify('Сначала дождитесь загрузки каталога.', 'error'); return false; }

async function request(path, options = {}) {
  const headers = { 'X-Editor-Token': token, ...options.headers };
  if (options.body) headers['Content-Type'] = 'application/json';
  const response = await fetch(path, { ...options, headers });
  if (response.status === 401) { showLogin(); throw new Error('Введите ключ доступа к редактору.'); }
  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    throw new Error(data.error || data.errors?.join('\n') || `Ошибка сервера: ${response.status}`);
  }
  return response;
}
const getJson = async (path, options) => (await request(path, options)).json();

function showLogin() {
  const body = modal('Вход в редактор'); body.append(element('p', 'dialog-help', 'Введите ключ, который выдал администратор редактора.'));
  const input = field(body, 'Ключ доступа', '', { type: 'password', max: 1000 });
  body.append(button('Продолжить', async () => {
    token = input.value; sessionStorage.setItem('economy-token', token);
    if (!state.catalog) { $('modal').close(); await start(); return; }
    try { await getJson('/api/projects'); $('modal').close(); notify('Доступ восстановлен. Можно продолжить работу с текущим проектом.', 'success'); }
    catch (e) { notify(e.message, 'error'); }
  }, 'primary')); input.focus();
}

function newProject() {
  return { schemaVersion: 1, id: '', title: 'Новый ассортимент', author: localStorage.getItem('economy-author') || '', notes: '', catalogVersion: state.catalog.version, revision: 0, vendors: clone(state.catalog.vendors) };
}
function setProject(project, dirty = false) {
  clearTimeout(localTimer);
  state.project = project; state.vendor = project.vendors[0]?.id || ''; state.tab = 0; state.page = 0; state.dirty = dirty; state.change++;
  $('project-title').value = project.title; $('project-author').value = project.author; $('project-notes').value = project.notes;
  $('dirty-indicator').hidden = !dirty; renderVendors(); renderEditor(); renderCatalog(); renderStale();
}
function changed(refreshVendors = true) {
  state.dirty = true; state.change++; $('dirty-indicator').hidden = false;
  clearTimeout(localTimer); localTimer = setTimeout(() => { try { localStorage.setItem(LOCAL_KEY, JSON.stringify(state.project)); } catch { notify('Не удалось записать локальный черновик. Сохраните проект на сервере или скачайте JSON.', 'error'); } }, 400);
  if (refreshVendors) renderVendors(); updateTotals();
}
function renderStale() {
  const stale = state.project.catalogVersion !== state.catalog.version;
  $('stale-banner').hidden = !stale;
  $('stale-banner').textContent = stale ? 'Проект создан по другой версии каталога. При экспорте будут проверены все предметы и доступы. Отсутствующие предметы отмечены в ассортименте.' : '';
}
function accountName(id) { return state.catalog.accounts.find(a => a.id === id)?.name || id || 'Казначейство'; }
function isChanged(v) { const original = state.catalog.vendors.find(x => x.id === v.id); return !original || JSON.stringify(original) !== JSON.stringify(v); }

function renderVendors() {
  if (!state.project) return;
  const search = $('vendor-search').value.trim().toLowerCase(), list = $('vendors'); list.replaceChildren();
  $('vendor-count').textContent = state.project.vendors.length;
  for (const v of state.project.vendors.filter(v => `${v.name} ${v.id}`.toLowerCase().includes(search))) {
    const b = button('', () => { state.vendor = v.id; state.tab = 0; renderVendors(); renderEditor(); renderCatalog(); }, `vendor-button${v.id === state.vendor ? ' active' : ''}`);
    b.setAttribute('aria-pressed', String(v.id === state.vendor));
    const canvas = element('canvas'); canvas.width = canvas.height = 40; drawIcon(canvas, v.id);
    const label = element('span', '', v.name); label.append(element('small', '', accountName(v.revenueAccount)));
    b.append(canvas, label); if (isChanged(v)) b.append(element('span', 'changed', '•')); list.append(b);
  }
  if (!list.childElementCount) list.append(element('p', 'muted', 'Автоматы не найдены.'));
}
function renderEditor() {
  const v = currentVendor(); if (!v) return;
  if (state.tab >= v.tabs.length) state.tab = v.tabs.length ? 0 : -1;
  $('vendor-name').textContent = v.name; $('vendor-id').textContent = v.id; $('department-name').textContent = accountName(v.revenueAccount).toUpperCase(); drawIcon($('vendor-icon'), v.id);
  const tabs = $('tabs'); tabs.replaceChildren();
  v.tabs.forEach((tab, index) => { const b = button(tab.labelRu || tab.name, () => { state.tab = index; renderEditor(); renderCatalog(); }, index === state.tab ? 'active' : ''); b.role = 'tab'; b.setAttribute('aria-selected', String(index === state.tab)); tabs.append(b); });
  const contraband = button(`Контрабанда · ${v.contrabandInventory.length}`, () => { state.tab = -1; renderEditor(); renderCatalog(); }, `contraband ${state.tab === -1 ? 'active' : ''}`); contraband.role = 'tab'; contraband.setAttribute('aria-selected', String(state.tab === -1)); tabs.append(contraband);
  $('edit-tab-button').disabled = state.tab === -1;
  $('access-summary').textContent = state.tab === -1 ? 'Доступна после взлома автомата' : v.tabs[state.tab]?.access.length ? 'Доступ: ' + v.tabs[state.tab].access.map(a => state.catalog.accessLevels.find(x => x.id === a)?.name || a).join(', ') : 'Доступна всем';
  renderInventory();
}
function renderInventory() {
  const container = $('inventory'); container.replaceChildren();
  const entries = currentEntries();
  if (!entries.length) container.append($('empty-inventory').content.cloneNode(true));
  entries.forEach((entry, index) => {
    const row = element('div', 'inventory-row'), item = itemById.get(entry.itemId), product = element('div', `inventory-product${!item ? ' missing' : ''}`);
    const canvas = element('canvas'); canvas.width = canvas.height = 40; drawIcon(canvas, entry.itemId);
    const label = element('span', '', item?.name || 'Предмет отсутствует в каталоге'); label.append(element('code', '', entry.itemId)); product.append(canvas, label);
    row.append(product);
    for (const [key, max, title] of [['price', 100000000, 'Цена'], ['stock', 1000000, 'Начальный запас']]) {
      const input = element('input'); input.type = 'number'; input.min = '0'; input.max = String(max); input.step = '1'; input.value = entry[key]; input.required = true; input.setAttribute('aria-label', `${title}: ${item?.name || entry.itemId}`);
      input.addEventListener('input', () => { if (input.validity.valid) { entry[key] = Number(input.value); changed(); } }); row.append(input);
    }
    row.append(button('×', () => { entries.splice(index, 1); changed(); renderInventory(); renderCatalog(); }, 'remove-button')); container.append(row);
  }); updateTotals();
}
function updateTotals() {
  const entries = currentEntries(); $('inventory-count').textContent = `${entries.length} позиций · ${money(entries.reduce((sum, e) => sum + e.stock, 0))} шт.`;
  $('inventory-value').replaceChildren(document.createTextNode(money(entries.reduce((sum, e) => sum + e.price * e.stock, 0)) + ' '), element('small', '', 'кр.'));
}
function checkInputs() { for (const input of document.querySelectorAll('#workspace input')) if (!input.reportValidity()) return false; return true; }
function addItem(id) {
  if (!ensureWritable()) return;
  const entries = currentEntries(); if (entries.some(e => e.itemId === id)) { notify('Этот предмет уже есть в выбранной вкладке. Измените его запас в таблице.'); return; }
  entries.push({ itemId: id, price: 100, stock: 5 }); changed(); renderInventory(); renderCatalog(); clearNotice();
}
function renderCatalog() {
  if (!state.catalog) return;
  const query = $('item-search').value.trim().toLowerCase(), category = $('category-filter').value;
  const filtered = state.catalog.items.filter(i => ($('hidden-filter').checked || !i.hidden) && ($('non-items-filter').checked || i.isItem) && (!category || i.category === category) && `${i.name} ${i.id}`.toLowerCase().includes(query));
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE)); state.page = Math.min(state.page, pages - 1);
  $('result-count').textContent = `${money(filtered.length)} найдено`; $('page-label').textContent = `${state.page + 1} / ${pages}`; $('prev-page').disabled = !state.page; $('next-page').disabled = state.page >= pages - 1;
  const grid = $('items'); grid.replaceChildren(); const included = new Set(currentEntries().map(e => e.itemId));
  for (const item of filtered.slice(state.page * PAGE_SIZE, (state.page + 1) * PAGE_SIZE)) {
    const card = button('', () => addItem(item.id), `item-card${included.has(item.id) ? ' included' : ''}`); card.draggable = true;
    card.title = `${item.name}\n${item.id}\n\n${item.description}`; card.setAttribute('aria-label', `Добавить: ${item.name} (${item.id})`);
    card.addEventListener('dragstart', e => { e.dataTransfer.setData('text/plain', item.id); e.dataTransfer.effectAllowed = 'copy'; });
    const canvas = element('canvas'); canvas.width = canvas.height = 64; drawIcon(canvas, item.id);
    card.append(canvas, element('span', 'item-name', item.name || item.id), element('span', 'add-sign', included.has(item.id) ? '✓' : '+')); grid.append(card);
  }
  if (!filtered.length) grid.append(element('p', 'empty-state', 'Ничего не найдено. Попробуйте другое название или ID.'));
}

function editTab(create = false) {
  const v = currentVendor(); if (!v) return;
  const original = create ? null : v.tabs[state.tab];
  const body = modal(create ? 'Новая вкладка' : 'Настройка вкладки');
  const ru = field(body, 'Название на русском', original?.labelRu || ''), en = field(body, 'Название на английском', original?.labelEn || '');
  body.append(element('p', 'dialog-help', 'Если выбрано несколько доступов, достаточно любого из них. Без ограничений вкладка доступна всем.'));
  const accesses = element('div', 'access-grid');
  for (const a of state.catalog.accessLevels) { const l = element('label'), input = element('input'); input.type = 'checkbox'; input.value = a.id; input.checked = original?.access.includes(a.id) || false; l.append(input, document.createTextNode(a.name || a.id)); l.title = a.id; accesses.append(l); }
  body.append(accesses); const actions = element('div', 'dialog-actions');
  if (original) actions.append(button('Удалить вкладку', () => { if (original.inventory.length && !confirm('Удалить вкладку вместе с её товарами?')) return; v.tabs.splice(state.tab, 1); state.tab = v.tabs.length ? 0 : -1; changed(); $('modal').close(); renderEditor(); renderCatalog(); }, 'danger'));
  actions.append(button('Сохранить', () => {
    if (!ru.value.trim() || !en.value.trim() || /[\r\n{}]/.test(ru.value + en.value)) { notify('Заполните оба названия одной строкой, без фигурных скобок.', 'error'); return; }
    const renamed = !original || original.labelRu !== ru.value.trim() || original.labelEn !== en.value.trim();
    const tab = { name: renamed ? 'is14-vending-tab-custom-' + crypto.randomUUID().replaceAll('-', '') : original.name, labelRu: ru.value.trim(), labelEn: en.value.trim(), access: [...accesses.querySelectorAll('input:checked')].map(e => e.value), inventory: original?.inventory || [] };
    if (create) { v.tabs.push(tab); state.tab = v.tabs.length - 1; } else v.tabs[state.tab] = tab;
    changed(); $('modal').close(); renderEditor(); renderCatalog();
  }, 'primary')); body.append(actions); ru.focus();
}
function vendorSettings() {
  const v = currentVendor(); if (!v) return;
  const body = modal('Параметры автомата'); body.append(element('p', 'dialog-help', v.name));
  const label = element('label', 'form-field', 'Счёт получателя выручки'), account = element('select');
  account.append(new Option('Казначейство (по умолчанию)', '')); for (const a of state.catalog.accounts) account.append(new Option(a.name || a.id, a.id)); account.value = v.revenueAccount; label.append(account); body.append(label);
  const tax = field(body, 'Доля выручки в казначейство, %', String(Number((v.taxPercent * 100).toFixed(4))), { type: 'number' }); tax.min = '0'; tax.max = '100'; tax.step = '0.01';
  const copyLabel = element('label', 'form-field', 'Скопировать все вкладки и товары из другого автомата'), source = element('select'); source.append(new Option('Выберите автомат…', '')); for (const other of state.project.vendors.filter(x => x.id !== v.id)) source.append(new Option(other.name, other.id)); copyLabel.append(source); body.append(copyLabel);
  body.append(button('Скопировать наполнение', () => { const other = state.project.vendors.find(x => x.id === source.value); if (!other || !confirm('Заменить все вкладки, товары и контрабанду выбранного автомата?')) return; v.tabs = clone(other.tabs); v.contrabandInventory = clone(other.contrabandInventory); state.tab = 0; changed(); $('modal').close(); renderEditor(); renderCatalog(); }));
  const actions = element('div', 'dialog-actions'); actions.append(button('Сохранить параметры', () => { if (!tax.reportValidity()) return; v.revenueAccount = account.value; v.taxPercent = Number(tax.value) / 100; changed(); $('modal').close(); renderEditor(); }, 'primary')); body.append(actions);
}
async function save() {
  if (!ensureWritable() || !checkInputs() || state.saving) return;
  state.saving = true; $('save-button').disabled = true;
  const target = state.project, snapshot = clone(target), change = state.change;
  try {
    const saved = await getJson(snapshot.id ? `/api/projects/${snapshot.id}` : '/api/projects', { method: snapshot.id ? 'PUT' : 'POST', body: JSON.stringify(snapshot) });
    if (state.project === target) {
      Object.assign(state.project, { id: saved.id, revision: saved.revision, updatedAt: saved.updatedAt });
      if (change === state.change) { state.dirty = false; $('dirty-indicator').hidden = true; clearTimeout(localTimer); localStorage.removeItem(LOCAL_KEY); }
      else localStorage.setItem(LOCAL_KEY, JSON.stringify(state.project));
    }
    notify(`Проект сохранён · версия ${saved.revision}`, 'success'); updateProjectCount();
  } catch (e) { notify(e.message, 'error'); }
  finally { state.saving = false; $('save-button').disabled = false; }
}
function download(blob, name) { const a = element('a'); const url = URL.createObjectURL(blob); a.href = url; a.download = name; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); }
async function exportProject() {
  if (!ensureWritable() || !checkInputs()) return;
  try { const response = await request('/api/export', { method: 'POST', body: JSON.stringify(state.project) }); download(await response.blob(), 'is14-assortment.zip'); notify('Экспорт готов. В архиве находятся проект, фрагменты YAML, локализация и инструкция применения.', 'success'); } catch (e) { notify(e.message, 'error'); }
}
async function updateProjectCount() { try { const projects = await getJson('/api/projects'); $('project-count').textContent = projects.length || ''; return projects; } catch { return []; } }
async function projectList() {
  const projects = await updateProjectCount(), body = modal('Общие проекты');
  body.append(button('＋ Новый проект из текущего каталога', () => { if (!confirmReplace()) return; setProject(newProject()); $('modal').close(); clearNotice(); }));
  if (!projects.length) body.append(element('p', 'dialog-help', 'Сохранённых проектов пока нет. Соберите ассортимент и нажмите «Сохранить проект».'));
  for (const p of projects) {
    const row = element('div', 'project-card'), description = element('div'); description.append(element('strong', '', p.title), element('small', '', `${p.author || 'Без автора'} · версия ${p.revision} · ${new Date(p.updatedAt).toLocaleString('ru-RU')}`));
    const actions = element('div', 'project-card-actions'); actions.append(button('Открыть', async () => { if (!confirmReplace()) return; try { setProject(await getJson(`/api/projects/${p.id}`)); $('modal').close(); clearNotice(); } catch (e) { notify(e.message, 'error'); } }), button('История', () => history(p)));
    row.append(description, actions); body.append(row);
  }
}
async function history(project) {
  try {
    const revisions = await getJson(`/api/projects/${project.id}/history`), body = modal(`История: ${project.title}`);
    body.append(element('p', 'dialog-help', 'Предыдущие версии открываются как новый проект. Текущая общая версия останется сохранена.'));
    for (const revision of revisions) body.append(button(`Версия ${revision}`, async () => { if (!confirmReplace()) return; try { const p = await getJson(`/api/projects/${project.id}/history/${revision}`); p.id = ''; p.revision = 0; p.title += ` · версия ${revision}`; setProject(p, true); changed(); $('modal').close(); } catch (e) { notify(e.message, 'error'); } }));
  } catch (e) { notify(e.message, 'error'); }
}

// Static first-frame previews of resolved layers; no YAML parsing or inheritance in the browser.
const texturePath = path => '/snapshot/textures/' + String(path).replaceAll('\\', '/').replace(/^\/?Textures\//, '').replace(/^\//, '').split('/').map(encodeURIComponent).join('/');
function loadImage(path) { if (!images.has(path)) images.set(path, new Promise((resolve, reject) => { const i = new Image(); i.onload = () => resolve(i); i.onerror = reject; i.src = texturePath(path); })); return images.get(path); }
function rsiMeta(path) { if (!metadata.has(path)) metadata.set(path, fetch(texturePath(path.replace(/\/$/, '') + '/meta.json')).then(r => { if (!r.ok) throw Error('Missing RSI'); return r.json(); })); return metadata.get(path); }
function vec(value, fallback) { if (Array.isArray(value)) return value.map(Number); if (typeof value === 'string') { const p = value.split(',').map(Number); if (p.length === 2 && p.every(Number.isFinite)) return p; } return fallback; }
async function composeIcon(id) {
  const item = itemById.get(id), icon = item?.icon; if (!icon) throw Error('No icon');
  const parts = icon.layers?.length ? icon.layers : [icon], rendered = [];
  for (const layer of parts) {
    if (layer.visible === false || layer.visible === 'false') continue;
    try {
      const sprite = layer.sprite || icon.sprite;
      let path = layer.texture || (!String(sprite).endsWith('.rsi') ? sprite : null), width, height;
      if (!path && sprite) { const meta = await rsiMeta(sprite); const name = layer.state || icon.state || meta.states[0]?.name; path = `${sprite}/${name}.png`; width = meta.size.x; height = meta.size.y; }
      if (!path) continue;
      const image = await loadImage(path); width ||= image.width; height ||= image.height;
      const canvas = document.createElement('canvas'); canvas.width = width; canvas.height = height; const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0, width, height, 0, 0, width, height);
      const color = layer.color || icon.color;
      if (color && color !== '#FFFFFF' && color !== '#ffffff' && color !== '#FFFFFFFF' && CSS.supports('color', color)) { ctx.globalCompositeOperation = 'multiply'; ctx.fillStyle = color; ctx.fillRect(0, 0, width, height); ctx.globalCompositeOperation = 'destination-in'; ctx.drawImage(image, 0, 0, width, height, 0, 0, width, height); }
      rendered.push({ canvas, offset: vec(layer.offset, [0, 0]), scale: vec(layer.scale, [1, 1]), rotation: Number(layer.rotation || 0) });
    } catch { /* Unavailable dynamic layers leave the rest of the preview usable. */ }
  }
  if (!rendered.length) throw Error('No visible static layers');
  const output = document.createElement('canvas'); output.width = output.height = 128; const ctx = output.getContext('2d'); ctx.imageSmoothingEnabled = false;
  const extent = Math.max(32, ...rendered.map(p => Math.max(p.canvas.width * Math.abs(p.scale[0]) + Math.abs(p.offset[0]) * 64, p.canvas.height * Math.abs(p.scale[1]) + Math.abs(p.offset[1]) * 64)));
  const scale = Math.min(3, 112 / extent);
  ctx.translate(64, 64); ctx.scale(scale, scale);
  for (const p of rendered) { ctx.save(); ctx.translate(p.offset[0] * 32, -p.offset[1] * 32); ctx.rotate(-p.rotation * Math.PI / 180); ctx.scale(p.scale[0], p.scale[1]); ctx.drawImage(p.canvas, -p.canvas.width / 2, -p.canvas.height / 2); ctx.restore(); }
  return output;
}
async function drawIcon(canvas, id) {
  canvas.dataset.item = id; const ctx = canvas.getContext('2d'); ctx.clearRect(0, 0, canvas.width, canvas.height);
  try { if (!icons.has(id)) icons.set(id, composeIcon(id)); const image = await icons.get(id); if (canvas.dataset.item !== id) return; ctx.imageSmoothingEnabled = false; ctx.drawImage(image, 0, 0, canvas.width, canvas.height); }
  catch { if (canvas.dataset.item !== id) return; ctx.fillStyle = '#677d77'; ctx.font = `${canvas.width / 2}px monospace`; ctx.textAlign = 'center'; ctx.fillText('◇', canvas.width / 2, canvas.height * .68); }
}

async function start() {
  try {
    state.catalog = await getJson('/api/catalog'); itemById.clear(); for (const item of state.catalog.items) itemById.set(item.id, item);
    $('catalog-version').textContent = state.catalog.version; $('catalog-date').textContent = `${new Date(state.catalog.generatedAt).toLocaleDateString('ru-RU')} · ${state.catalog.items.length} сущностей`;
    setProject(newProject()); $('workspace').setAttribute('aria-busy', 'false'); updateProjectCount();
    $('restore-banner').hidden = !localStorage.getItem(LOCAL_KEY); clearNotice();
  } catch (e) { notify(e.message, 'error'); }
}
$('close-modal').onclick = () => $('modal').close();
$('vendor-search').oninput = renderVendors;
for (const id of ['item-search', 'category-filter', 'hidden-filter', 'non-items-filter']) $(id).addEventListener(id === 'item-search' ? 'input' : 'change', () => { state.page = 0; renderCatalog(); });
$('prev-page').onclick = () => { state.page--; renderCatalog(); }; $('next-page').onclick = () => { state.page++; renderCatalog(); };
$('add-tab-button').onclick = () => editTab(true); $('edit-tab-button').onclick = () => editTab(); $('settings-button').onclick = vendorSettings;
for (const [id, key] of [['project-title', 'title'], ['project-author', 'author'], ['project-notes', 'notes']]) $(id).oninput = () => { if (!state.project) return; state.project[key] = $(id).value; if (key === 'author') localStorage.setItem('economy-author', $(id).value); changed(false); };
$('save-button').onclick = save; $('export-button').onclick = exportProject; $('projects-button').onclick = projectList;
$('copy-project-button').onclick = () => { if (!ensureWritable()) return; const p = clone(state.project); p.id = ''; p.revision = 0; p.title += ' · копия'; setProject(p, true); changed(); notify('Создана отдельная копия. Сохраните её, чтобы поделиться с другими дизайнерами.'); };
$('json-button').onclick = () => { if (ensureWritable()) download(new Blob([JSON.stringify(state.project, null, 2)], { type: 'application/json' }), 'is14-economy-project.json'); };
$('import-button').onclick = () => $('import-file').click();
$('import-file').onchange = async e => {
  try {
    const file = e.target.files[0]; if (!file) return; if (file.size > 8 * 1024 * 1024) throw Error('Файл должен быть меньше 8 МБ.');
    const p = JSON.parse(await file.text()); const checked = await getJson('/api/validate?draft=true', { method: 'POST', body: JSON.stringify(p) });
    if (checked.errors.length) throw Error(checked.errors.join('\n')); if (!confirmReplace()) return;
    p.id = ''; p.revision = 0; setProject(p, true); changed(); notify('Проект импортирован как новая копия.', 'success');
  } catch (e) { notify(e.message, 'error'); } finally { $('import-file').value = ''; }
};
$('restore-button').onclick = async () => { try { const p = JSON.parse(localStorage.getItem(LOCAL_KEY)); const result = await getJson('/api/validate?draft=true', { method: 'POST', body: JSON.stringify(p) }); if (result.errors.length) throw Error(result.errors.join('\n')); setProject(p, true); $('restore-banner').hidden = true; } catch (e) { notify('Не удалось восстановить черновик: ' + e.message, 'error'); } };
$('discard-restore').onclick = () => { localStorage.removeItem(LOCAL_KEY); $('restore-banner').hidden = true; };
$('bulk-price-button').onclick = () => { const value = prompt('Изменение цен во всей выбранной вкладке, в процентах. Например: 10 или -15.'); if (value === null || value.trim() === '') return; const percent = Number(value.replace(',', '.')); if (!Number.isFinite(percent) || percent < -100 || percent > 1000) { notify('Введите процент от -100 до 1000.', 'error'); return; } for (const e of currentEntries()) e.price = Math.min(100000000, Math.round(e.price * (1 + percent / 100))); changed(); renderInventory(); };
$('inventory').addEventListener('dragover', e => { e.preventDefault(); $('inventory').classList.add('drag-over'); });
$('inventory').addEventListener('dragleave', e => { if (!$('inventory').contains(e.relatedTarget)) $('inventory').classList.remove('drag-over'); });
$('inventory').addEventListener('drop', e => { e.preventDefault(); $('inventory').classList.remove('drag-over'); const id = e.dataTransfer.getData('text/plain'); if (itemById.has(id)) addItem(id); });
window.addEventListener('beforeunload', e => { if (state.dirty) { e.preventDefault(); e.returnValue = ''; } });
start();
