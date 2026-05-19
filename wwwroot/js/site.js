// ── Tab switching ──
function switchTab(id) {
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.getElementById('tab-' + id)?.classList.add('active');
  document.querySelectorAll('.tab-btn').forEach(b => {
    if (b.getAttribute('onclick')?.includes("'" + id + "'")) b.classList.add('active');
  });
}

// ── Sample sentences ──
function setSample(inputName, value) {
  const el = document.querySelector(`input[name="${inputName}"], textarea[name="${inputName}"]`);
  if (el) el.value = value;
}

function setSampleTA(value) {
  const ta = document.querySelector('textarea[name="AisParseInput"]');
  if (ta) ta.value = value.replace(/\\n/g, '\n');
}

// ── Copy to clipboard ──
function copyText(text) {
  navigator.clipboard.writeText(text).then(() => {
    const btn = event?.target;
    if (btn) {
      const orig = btn.textContent;
      btn.textContent = 'Copied!';
      btn.style.color = '#00e87a';
      setTimeout(() => { btn.textContent = orig; btn.style.color = ''; }, 1500);
    }
  }).catch(() => {
    const ta = document.createElement('textarea');
    ta.value = text; ta.style.position = 'fixed'; ta.style.opacity = '0';
    document.body.appendChild(ta); ta.select();
    document.execCommand('copy'); document.body.removeChild(ta);
  });
}

// Persist active tab across form submits via URL hash
document.addEventListener('DOMContentLoaded', () => {
  // highlight number inputs on focus
  document.querySelectorAll('.form-input[type=number]').forEach(inp => {
    inp.addEventListener('focus', () => inp.select());
  });
});
