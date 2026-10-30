import { api } from './api.js?v=render-1';
import { el, heading, field } from './ui.js?v=render-1';

export async function requireAdmin(main) {
  const session = await api.get('auth/session');
  if (session.authenticated) return true;
  const username = el('input', { name: 'username', autocomplete: 'username', required: true, maxlength: 100, value: 'admin' });
  const password = el('input', { name: 'password', type: 'password', autocomplete: 'current-password', required: true, maxlength: 1024 });
  const message = el('p', { class: 'inline-error', role: 'alert', hidden: true });
  const submit = el('button', { type: 'submit', class: 'button' }, 'შესვლა');
  const form = el('form', { class: 'admin-form form-section', style: 'max-width:480px' },
    field('მომხმარებლის სახელი', username), field('პაროლი', password), message, submit);
  form.addEventListener('submit', async event => {
    event.preventDefault(); if (!form.reportValidity()) return;
    submit.disabled = true; submit.textContent = 'შესვლა მიმდინარეობს…'; message.hidden = true;
    try { await api.post('auth/login', { username: username.value, password: password.value }); password.value = ''; location.reload(); }
    catch (error) { message.textContent = error.status === 401 ? 'სახელი ან პაროლი არასწორია.' : error.status === 429 ? 'ძალიან ბევრი მცდელობაა. სცადე ერთ წუთში.' : error.message; message.hidden = false; }
    finally { submit.disabled = false; submit.textContent = 'შესვლა'; }
  });
  main.replaceChildren(heading('ადმინისტრატორის შესვლა', 'მართვა, მომხმარებლები და შეკვეთები დაცულია. სასწავლო შეკვეთის გასაფორმებლადაც საჭიროა ადმინისტრატორის შესვლა.'), form);
  return false;
}
