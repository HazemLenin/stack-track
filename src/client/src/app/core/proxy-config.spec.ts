import proxy from '../../../proxy.conf.json';

describe('proxy.conf.json', () => {
  it('forwards /api to the local API without the prefix', () => {
    const api = proxy['/api'];
    expect(api.target).toBe('http://localhost:5009');
    expect(api.pathRewrite['^/api']).toBe('');
    expect(api.changeOrigin).toBe(true);
  });
});
