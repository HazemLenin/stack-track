import { TestBed } from '@angular/core/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatMenuHarness } from '@angular/material/menu/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renders toolbar, user menu button and router outlet', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('mat-toolbar')?.textContent).toContain('Stack Track');
    expect(el.querySelector('button[aria-label="User menu"]')).toBeTruthy();
    expect(el.querySelector('main router-outlet')).toBeTruthy();
  });

  it('shows a disabled sign in item in the user menu', async () => {
    const fixture = TestBed.createComponent(App);
    const loader = TestbedHarnessEnvironment.loader(fixture);
    const menu = await loader.getHarness(MatMenuHarness);
    await menu.open();
    const items = await menu.getItems();
    expect(items.length).toBe(1);
    expect(await items[0].getText()).toContain('Sign in (coming soon)');
    expect(await items[0].isDisabled()).toBe(true);
  });
});
