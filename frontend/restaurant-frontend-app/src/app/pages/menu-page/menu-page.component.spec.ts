import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Params, provideRouter, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { MenuPageComponent } from './menu-page.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';

const SALAD: DishListResponse = {
  id: 'salad-1',
  name: 'Caesar Salad',
  imageUrl: 'assets/img/dish.png',
  price: 18,
  state: 'Available',
  weight: '200g',
  dishType: 'Appetizer',
  description: 'Salad',
  calories: '100',
  proteins: '5',
  carbohydrates: '10',
  fats: '4',
  vitamins: 'A',
};

describe('MenuPageComponent search', () => {
  let fixture: ComponentFixture<MenuPageComponent>;
  let http: HttpTestingController;
  let parameters: BehaviorSubject<Params>;
  let navigate: jasmine.Spy;

  beforeEach(async () => {
    parameters = new BehaviorSubject<Params>({});
    await TestBed.configureTestingModule({
      imports: [MenuPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: ActivatedRoute, useValue: { queryParams: parameters.asObservable() } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  afterEach(() => {
    fixture?.destroy();
    http.verify();
  });

  function start(params: Params = {}): void {
    parameters.next(params);
    fixture = TestBed.createComponent(MenuPageComponent);
    fixture.detectChanges();
  }

  function request() {
    return http.expectOne((pending) => pending.url === 'https://test.api/dishes');
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function inputSearch(value: string): void {
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      'input[type="search"]'
    );
    expect(input).withContext('Menu exposes a search field').not.toBeNull();
    if (!input) return;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function submitSearch(): void {
    const form = (fixture.nativeElement as HTMLElement).querySelector('form[role="search"]');
    expect(form).withContext('Search supports form submission and Enter').not.toBeNull();
    form?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  }

  it('restores URL search and combines name, category and sorting in the API request', () => {
    start({ q: '  Salad  ', category: 'Appetizer', sort: 'PriceAscending' });
    const pending = request();
    expect(pending.request.params.get('name')).toBe('Salad');
    expect(pending.request.params.get('dishType')).toBe('Appetizer');
    expect(pending.request.params.get('sortBy')).toBe('PriceAscending');
    pending.flush([SALAD]);
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      'input[type="search"]'
    );
    expect(input?.value).toBe('Salad');
    expect(text()).toContain('Caesar Salad');
  });

  it('submits trimmed text to the URL while retaining reservation and filter parameters', () => {
    start({ preOrder: 'true', reservationId: 'reservation-1', category: 'Appetizer' });
    request().flush([SALAD]);
    fixture.detectChanges();
    inputSearch('  Salad & soup  ');
    submitSearch();
    expect(navigate).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({
        queryParams: { q: 'Salad & soup' },
        queryParamsHandling: 'merge',
      })
    );
    expect(fixture.componentInstance.preOrderReservationId).toBe('reservation-1');
  });

  it('does not issue a request for every draft keystroke', () => {
    start();
    request().flush([]);
    inputSearch('Sal');
    inputSearch('Salad');
    http.expectNone((pending) => pending.url === 'https://test.api/dishes');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('removes the name filter for a whitespace-only submission', () => {
    start({ q: 'Salad' });
    request().flush([]);
    inputSearch('   ');
    submitSearch();
    expect(navigate).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({
        queryParams: { q: null },
        queryParamsHandling: 'merge',
      })
    );
  });

  it('clears an active query without resetting category or sort', () => {
    start({ q: 'Salad', category: 'Appetizer', sort: 'PriceAscending' });
    request().flush([SALAD]);
    fixture.detectChanges();
    const clear = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button')
    ).find((button) => button.textContent?.trim() === 'Clear search');
    expect(clear).not.toBeUndefined();
    clear?.click();
    expect(navigate).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({
        queryParams: { q: null },
        queryParamsHandling: 'merge',
      })
    );
  });

  it('shows loading until results arrive and announces the result count', () => {
    start({ q: 'Salad' });
    expect(text()).toContain('Loading dishes');
    request().flush([SALAD]);
    fixture.detectChanges();
    expect(text()).not.toContain('Loading dishes');
    expect(text()).toContain('1 dish found');
  });

  it('distinguishes no search matches from an empty menu', () => {
    start({ q: 'missing' });
    request().flush([]);
    fixture.detectChanges();
    expect(text()).toContain('No dishes match');
    expect(text()).toContain('missing');
    parameters.next({});
    request().flush([]);
    fixture.detectChanges();
    expect(text()).toContain('No dishes available');
  });

  it('recovers from an API failure through Try again without losing the search', () => {
    start({ q: 'Salad' });
    request().flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(text()).toContain('Could not load dishes');
    const retry = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button')
    ).find((button) => button.textContent?.trim() === 'Try again');
    expect(retry).not.toBeUndefined();
    retry?.click();
    if (!retry) return;
    const pending = request();
    expect(pending.request.params.get('name')).toBe('Salad');
    pending.flush([SALAD]);
    fixture.detectChanges();
    expect(text()).toContain('Caesar Salad');
    expect(text()).not.toContain('Could not load dishes');
  });

  it('continues loading a different query after a failed search', () => {
    start({ q: 'Salad' });
    request().flush({}, { status: 503, statusText: 'Unavailable' });
    parameters.next({ q: 'Soup' });
    const pending = request();
    expect(pending.request.params.get('name')).toBe('Soup');
    pending.flush([{ ...SALAD, id: 'soup-1', name: 'Tom Yum Soup' }]);
    fixture.detectChanges();
    expect(text()).toContain('Tom Yum Soup');
    expect(text()).not.toContain('Could not load dishes');
  });

  it('cancels an old search when URL navigation selects a new one', () => {
    start({ q: 'Salad' });
    const old = request();
    parameters.next({ q: 'Soup' });
    expect(old.cancelled).toBeTrue();
    const current = request();
    expect(current.request.params.get('name')).toBe('Soup');
    current.flush([{ ...SALAD, name: 'Tom Yum Soup' }]);
    fixture.detectChanges();
    expect(text()).toContain('Tom Yum Soup');
    expect(text()).not.toContain('Caesar Salad');
  });

  it('cancels HTTP and route subscriptions when leaving the page', () => {
    start({ q: 'Salad' });
    const pending = request();
    fixture.destroy();
    expect(pending.cancelled).toBeTrue();
    parameters.next({ q: 'Soup' });
    http.expectNone((next) => next.url === 'https://test.api/dishes');
  });

  it('retains the existing dish detail interaction', () => {
    start({ q: 'Salad' });
    request().flush([SALAD]);
    fixture.detectChanges();
    const card = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.dish-card');
    card?.click();
    expect(fixture.componentInstance.selectedDish?.id).toBe('salad-1');
  });
});
