import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { DishesService } from './dishes.service';

describe('DishesService search contract', () => {
  let http: HttpTestingController;
  let service: DishesService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(DishesService);
  });

  afterEach(() => http.verify());

  it('keeps special characters in the name rather than treating them as query parameters', () => {
    service.getDishes('Appetizer', 'PriceAscending', 'Салат & Soup+?').subscribe();
    const pending = http.expectOne((request) => request.url === 'https://test.api/dishes');
    expect(pending.request.method).toBe('GET');
    const url = new URL(pending.request.urlWithParams);
    expect(url.searchParams.get('name')).toBe('Салат & Soup+?');
    expect(url.searchParams.get('dishType')).toBe('Appetizer');
    expect(url.searchParams.get('sortBy')).toBe('PriceAscending');
    pending.flush([]);
  });
});
