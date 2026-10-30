export class ApiError extends Error {
  constructor(message, status = 0) { super(message); this.status = status; }
}

async function request(path, method = 'GET', body) {
  try {
    const response = await fetch(`/api/${path}`, {
      method, cache: 'no-store', signal: AbortSignal.timeout(path === 'catalog-import/dummyjson' ? 45000 : 20000),
      headers: { Accept: 'application/json', ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
      ...(body === undefined ? {} : { body: JSON.stringify(body) })
    });
    if (response.status === 204) return null;
    const data = await response.json().catch(() => null);
    if (!response.ok) {
      const validation = data?.errors ? Object.values(data.errors).flat().join(' ') : '';
      const fallback = response.status === 404 ? 'ჩანაწერი ვერ მოიძებნა.' : 'მოქმედება ვერ შესრულდა. სცადე ხელახლა.';
      throw new ApiError(validation || data?.detail || fallback, response.status);
    }
    if (data === null) throw new ApiError('სერვერის პასუხი არასწორია. განაახლე გვერდი.');
    return data;
  } catch (error) {
    if (error instanceof ApiError) throw error;
    throw new ApiError('სერვერთან კავშირი ვერ დამყარდა. გადაამოწმე ინტერნეტი და სცადე ხელახლა. შეკვეთის გაგზავნისას შედეგი ჯერ შეკვეთების გვერდზე შეამოწმე.');
  }
}

export const api = {
  get: path => request(path),
  post: (path, data) => request(path, 'POST', data),
  put: (path, data) => request(path, 'PUT', data),
  delete: path => request(path, 'DELETE')
};
