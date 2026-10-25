import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { AdminUser, AdminUsersService, UserPage } from './admin-users.service';
import { AdminUsersPage } from './admin-users-page';

const user = (id: string, over: Partial<AdminUser> = {}): AdminUser => ({
  id,
  displayName: `User ${id}`,
  email: `${id}@example.com`,
  role: 'Member',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  ...over,
});

const page = (items: AdminUser[], totalCount = items.length): UserPage => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount,
});

describe('AdminUsersPage', () => {
  let fixture: ComponentFixture<AdminUsersPage>;
  let el: HTMLElement;
  let api: {
    list: ReturnType<typeof vi.fn>;
    deactivate: ReturnType<typeof vi.fn>;
    reactivate: ReturnType<typeof vi.fn>;
    changeRole: ReturnType<typeof vi.fn>;
  };
  let confirmed: boolean;

  beforeEach(async () => {
    vi.useFakeTimers();
    confirmed = true;
    api = {
      list: vi.fn(() => of(page([user('me', { role: 'Admin' }), user('omar'), user('gone', { isActive: false })], 45))),
      deactivate: vi.fn(() => of(undefined)),
      reactivate: vi.fn(() => of(undefined)),
      changeRole: vi.fn(() => of(undefined)),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: AdminUsersService, useValue: api },
        { provide: AuthService, useValue: { user: signal({ id: 'me' }) } },
        { provide: MatDialog, useValue: { open: () => ({ afterClosed: () => of(confirmed) }) } },
      ],
    });
    fixture = TestBed.createComponent(AdminUsersPage);
    el = fixture.nativeElement;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => vi.useRealTimers());

  const rows = () => Array.from(el.querySelectorAll('tr[mat-row]')) as HTMLElement[];
  const button = (row: HTMLElement, label: string) =>
    Array.from(row.querySelectorAll('button')).find((b) => b.textContent?.trim() === label) as HTMLButtonElement;

  it('loads page 1 sorted by name and renders rows', () => {
    expect(api.list).toHaveBeenCalledWith({ search: '', sort: 'name', dir: 'asc', page: 1 });
    expect(rows().length).toBe(3);
    expect(rows()[1].textContent).toContain('omar@example.com');
    expect(rows()[2].textContent).toContain('Deactivated');
    expect(fixture.debugElement.query(By.directive(MatPaginator)).componentInstance.pageSize).toBe(20);
  });

  it('searches after a debounce and returns to page 1', async () => {
    const input = el.querySelector('input[type=search]') as HTMLInputElement;
    input.value = 'omar';
    input.dispatchEvent(new Event('input'));
    expect(api.list).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(300);
    expect(api.list).toHaveBeenLastCalledWith({ search: 'omar', sort: 'name', dir: 'asc', page: 1 });
  });

  it('reloads when the sort header or paginator changes', () => {
    fixture.debugElement.query(By.directive(MatSort)).injector.get(MatSort).sortChange.emit({ active: 'created', direction: 'desc' });
    expect(api.list).toHaveBeenLastCalledWith({ search: '', sort: 'created', dir: 'desc', page: 1 });
    fixture.debugElement.query(By.directive(MatPaginator)).componentInstance.page.emit({ pageIndex: 1, pageSize: 20, length: 45 });
    expect(api.list).toHaveBeenLastCalledWith({ search: '', sort: 'created', dir: 'desc', page: 2 });
  });

  it('disables actions on own row', () => {
    const own = rows()[0];
    expect(button(own, 'Deactivate').disabled).toBe(true);
    expect((own.querySelector('select') as HTMLSelectElement).disabled).toBe(true);
    expect(button(rows()[1], 'Deactivate').disabled).toBe(false);
  });

  it('confirms, then deactivates and reloads', () => {
    confirmed = false;
    button(rows()[1], 'Deactivate').click();
    expect(api.deactivate).not.toHaveBeenCalled();
    confirmed = true;
    button(rows()[1], 'Deactivate').click();
    expect(api.deactivate).toHaveBeenCalledWith('omar');
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('reactivates a deactivated user', () => {
    button(rows()[2], 'Reactivate').click();
    expect(api.reactivate).toHaveBeenCalledWith('gone');
  });

  it('changes role from the select', () => {
    const select = rows()[1].querySelector('select') as HTMLSelectElement;
    select.value = 'Admin';
    select.dispatchEvent(new Event('change'));
    expect(api.changeRole).toHaveBeenCalledWith('omar', 'Admin');
  });
});
