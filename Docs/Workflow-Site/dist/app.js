(() => {
  'use strict';
  const { stages, sources } = globalThis.WORKFLOW_REPORT;
  const tabs = document.getElementById('stage-tabs');
  const panel = document.getElementById('stage-panel');
  const escapeHtml = text => text.replace(/[&<>"']/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
  const sourceButton = id => `<button class="source-link" data-source="${id}">${escapeHtml(sources[id].title)} ↗</button>`;
  stages.forEach((stage, index) => {
    const button = document.createElement('button');
    button.id = `stage-${index}`;
    button.type = 'button';
    button.setAttribute('role', 'tab');
    button.setAttribute('aria-controls', 'stage-panel');
    button.innerHTML = `<span>0${index + 1}</span>${escapeHtml(stage.name)}`;
    button.addEventListener('click', () => selectStage(index));
    button.addEventListener('keydown', event => {
      const positions = { ArrowRight: (index + 1) % stages.length, ArrowLeft: (index + stages.length - 1) % stages.length, Home: 0, End: stages.length - 1 };
      if (event.key in positions) {
        event.preventDefault();
        selectStage(positions[event.key]);
        tabs.children[positions[event.key]].focus();
      }
    });
    tabs.appendChild(button);
  });
  function selectStage(index) {
    Array.from(tabs.children).forEach((button, position) => {
      button.setAttribute('aria-selected', String(position === index));
      button.tabIndex = position === index ? 0 : -1;
    });
    const stage = stages[index];
    panel.setAttribute('aria-labelledby', `stage-${index}`);
    panel.innerHTML = `<div class="stage-summary"><span class="eyebrow">STEP 0${index + 1} / ${escapeHtml(stage.tag)}</span><h3>${escapeHtml(stage.name)}</h3><p>${escapeHtml(stage.intro)}</p><small>责任角色 · ${escapeHtml(stage.role)}</small></div><div class="stage-details"><dl><dt>输入</dt><dd>${escapeHtml(stage.input)}</dd><dt>动作</dt><dd>${escapeHtml(stage.action)}</dd><dt>输出</dt><dd>${escapeHtml(stage.output)}</dd></dl><p class="stage-boundary">边界 · ${escapeHtml(stage.stop)}</p><div class="stage-sources">${stage.sources.map(sourceButton).join('')}</div></div>`;
  }
  selectStage(0);
  const dialog = document.getElementById('source-dialog');
  const sourceList = document.getElementById('source-list');
  Object.entries(sources).forEach(([id, source]) => {
    const button = document.createElement('button');
    button.className = 'source-item';
    button.dataset.source = id;
    button.innerHTML = `${escapeHtml(source.title)} ↗<span>${escapeHtml(source.path)}</span>`;
    sourceList.appendChild(button);
  });
  document.addEventListener('click', event => {
    const trigger = event.target.closest('[data-source]');
    if (!trigger) return;
    const source = sources[trigger.dataset.source];
    if (!source) return;
    document.getElementById('source-title').textContent = source.title;
    document.getElementById('source-path').textContent = source.path;
    document.getElementById('source-excerpt').textContent = source.excerpt;
    const prefix = location.protocol === 'file:' ? '../../../' : '/source/';
    document.getElementById('source-original').href = prefix + source.path.split('/').map(encodeURIComponent).join('/');
    dialog.showModal();
  });
  document.getElementById('close-dialog').addEventListener('click', () => dialog.close());
  dialog.addEventListener('click', event => {
    if (event.target !== dialog) return;
    const box = dialog.getBoundingClientRect();
    if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) dialog.close();
  });
  const sections = Array.from(document.querySelectorAll('main > section'));
  const links = Array.from(document.querySelectorAll('nav a'));
  let queued = false;
  function updateNav() {
    const active = sections.filter(section => section.getBoundingClientRect().top <= 160).pop() || sections[0];
    links.forEach(link => {
      if (link.hash === `#${active.id}`) link.setAttribute('aria-current', 'location');
      else link.removeAttribute('aria-current');
    });
    queued = false;
  }
  window.addEventListener('scroll', () => {
    if (!queued) { queued = true; requestAnimationFrame(updateNav); }
  }, { passive: true });
  updateNav();
})();
