import { Vector, HashMap, WithEquality, Option, ToOrderable, Ordering, HashSet, areEqual } from "prelude-ts";

export class LazySeq<T> {
  static ofIterable<T>(iter: Iterable<T>): LazySeq<T> {
    return new LazySeq(iter);
  }

  static ofIterator<T>(f: () => Iterator<T>): LazySeq<T> {
    return new LazySeq<T>({
      [Symbol.iterator]() {
        return f();
      }
    });
  }

  static ofObject<V>(obj: { [k: string]: V }): LazySeq<[string, V]> {
    return LazySeq.ofIterator(function*() {
      for (let k in obj) {
        if (obj.hasOwnProperty(k)) {
          yield [k, obj[k]] as [string, V];
        }
      }
    });
  }

  static ofRange(start: number, end: number, step?: number): LazySeq<number> {
    const s = step || 1;
    return LazySeq.ofIterator(function*() {
      for (let x = start; x < end; x += s) {
        yield x;
      }
    });
  }

  [Symbol.iterator](): Iterator<T> {
    return this.iter[Symbol.iterator]();
  }

  allMatch(f: (x: T) => boolean): boolean {
    for (let x of this.iter) {
      if (!f(x)) {
        return false;
      }
    }
    return true;
  }

  anyMatch(f: (x: T) => boolean): boolean {
    for (let x of this.iter) {
      if (f(x)) {
        return true;
      }
    }
    return false;
  }

  append(x: T): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      yield* iter;
      yield x;
    });
  }

  appendAll(i: Iterable<T>): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      yield* iter;
      yield* i;
    });
  }

  arrangeBy<K>(f: (x: T) => K & WithEquality): Option<HashMap<K, T>> {
    let m = HashMap.empty<K, T>();
    for (let x of this.iter) {
      const k = f(x);
      if (m.containsKey(k)) {
        return Option.none();
      } else {
        m = m.put(k, x);
      }
    }
    return Option.some(m);
  }

  chunk(size: number): LazySeq<Vector<T>> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      let chunk = Vector.empty<T>();
      for (let x of iter) {
        chunk = chunk.append(x);
        if (chunk.length() === size) {
          yield chunk;
          chunk = Vector.empty<T>();
        }
      }
      if (chunk.length() > 0) {
        yield chunk;
      }
    });
  }

  contains(v: T & WithEquality): boolean {
    for (let x of this.iter) {
      if (areEqual(x, v)) {
        return true;
      }
    }
    return false;
  }

  drop(n: number): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      let cnt = 0;
      for (let x of iter) {
        if (cnt >= n) {
          yield x;
        } else {
          cnt += 1;
        }
      }
    });
  }

  dropWhile(f: (x: T) => boolean): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      let dropping = true;
      for (let x of iter) {
        if (dropping) {
          if (!f(x)) {
            dropping = false;
            yield x;
          }
        } else {
          yield x;
        }
      }
    });
  }

  isEmpty(): boolean {
    const first = this.iter[Symbol.iterator]().next();
    return first.done === true;
  }

  filter(f: (x: T) => boolean): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      for (let x of iter) {
        if (f(x)) {
          yield x;
        }
      }
    });
  }

  find(f: (v: T) => boolean): Option<T> {
    for (let x of this.iter) {
      if (f(x)) {
        return Option.of(x);
      }
    }
    return Option.none<T>();
  }

  flatMap<S>(f: (x: T) => Iterable<S>): LazySeq<S> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      for (let x of iter) {
        yield* f(x);
      }
    });
  }

  foldLeft<S>(zero: S, f: (soFar: S, cur: T) => S): S {
    let soFar = zero;
    for (let x of this.iter) {
      soFar = f(soFar, x);
    }
    return soFar;
  }

  groupBy<K>(f: (x: T) => K & WithEquality): HashMap<K, Vector<T>> {
    return this.toMap<K & WithEquality, Vector<T>>(
      x => [f(x), Vector.of(x)] as [K & WithEquality, Vector<T>],
      (v1, v2) => v1.appendAll(v2)
    );
  }

  head(): Option<T> {
    const first = this.iter[Symbol.iterator]().next();
    if (first.done) {
      return Option.none<T>();
    } else {
      return Option.some(first.value);
    }
  }

  length(): number {
    let cnt = 0;
    for (let _ of this.iter) {
      cnt += 1;
    }
    return cnt;
  }

  map<S>(f: (x: T, idx: number) => S): LazySeq<S> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      let idx = 0;
      for (let x of iter) {
        yield f(x, idx);
        idx += 1;
      }
    });
  }

  mapOption<S>(f: (x: T) => Option<S>): LazySeq<S> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      for (let x of iter) {
        const y = f(x);
        if (y.isSome()) {
          yield y.get();
        }
      }
    });
  }

  maxBy(compare: (v1: T, v2: T) => Ordering): Option<T> {
    return this.reduce((v1, v2) => (compare(v1, v2) < 0 ? v2 : v1));
  }

  maxOn(getOrderable: ToOrderable<T>): Option<T> {
    let ret = Option.none<T>();
    let maxVal: number | string | boolean | undefined;
    for (let x of this.iter) {
      if (ret.isNone()) {
        ret = Option.of(x);
        maxVal = getOrderable(x);
      } else {
        const curVal = getOrderable(x);
        if (!maxVal || maxVal < curVal) {
          ret = Option.of(x);
          maxVal = curVal;
        }
      }
    }
    return ret;
  }

  minBy(compare: (v1: T, v2: T) => Ordering): Option<T> {
    return this.reduce((v1, v2) => (compare(v1, v2) > 0 ? v2 : v1));
  }

  minOn(getOrderable: ToOrderable<T>): Option<T> {
    let ret = Option.none<T>();
    let minVal: number | string | boolean | undefined;
    for (let x of this.iter) {
      if (ret.isNone()) {
        ret = Option.of(x);
        minVal = getOrderable(x);
      } else {
        const curVal = getOrderable(x);
        if (!minVal || minVal > curVal) {
          ret = Option.of(x);
          minVal = curVal;
        }
      }
    }
    return ret;
  }

  prepend(x: T): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      yield x;
      yield* iter;
    });
  }

  prependAll(i: Iterable<T>): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      yield* i;
      yield* iter;
    });
  }

  reduce(f: (x1: T, x2: T) => T): Option<T> {
    let ret = Option.none<T>();
    for (let x of this.iter) {
      if (ret.isNone()) {
        ret = Option.of(x);
      } else {
        ret = Option.of(f(ret.get(), x));
      }
    }
    return ret;
  }

  single(): Option<T> {
    const iter = this.iter[Symbol.iterator]();
    const first = iter.next();
    if (first.done) {
      // empty
      return Option.none<T>();
    }
    const next = iter.next();
    if (next.done) {
      // singleton
      return Option.some(first.value);
    } else {
      // more than one
      return Option.none<T>();
    }
  }

  sumOn(getNumber: (v: T) => number): number {
    let sum = 0;
    for (let x of this.iter) {
      sum += getNumber(x);
    }
    return sum;
  }

  tail(): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      let seenFirst = false;
      for (let x of iter) {
        if (seenFirst) {
          yield x;
        } else {
          seenFirst = true;
        }
      }
    });
  }

  take(n: number): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      let cnt = 0;
      for (let x of iter) {
        if (cnt >= n) {
          return;
        }
        yield x;
        cnt += 1;
      }
    });
  }

  takeWhile(f: (x: T) => boolean): LazySeq<T> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      for (let x of iter) {
        if (f(x)) {
          yield x;
        } else {
          return;
        }
      }
    });
  }

  toArray(): Array<T> {
    return Array.from(this.iter);
  }

  toMap<K, S>(f: (x: T) => [K & WithEquality, S], merge: (v1: S, v2: S) => S): HashMap<K, S> {
    let m = HashMap.empty<K, S>();
    for (let x of this.iter) {
      const [k, s] = f(x);
      m = m.putWithMerge(k, s, merge);
    }
    return m;
  }

  toSet<S>(converter: (x: T) => S & WithEquality): HashSet<S> {
    const iter = this.iter;
    return HashSet.ofIterable({
      *[Symbol.iterator]() {
        for (let x of iter) {
          yield converter(x);
        }
      }
    });
  }

  toVector(): Vector<T> {
    return Vector.ofIterable(this.iter);
  }

  transform<U>(f: (s: LazySeq<T>) => U): U {
    return f(this);
  }

  zip<S>(other: Iterable<S>): LazySeq<[T, S]> {
    const iter = this.iter;
    return LazySeq.ofIterator(function*() {
      const i1 = iter[Symbol.iterator]();
      const i2 = other[Symbol.iterator]();
      while (true) {
        const n1 = i1.next();
        const n2 = i2.next();
        if (n1.done || n2.done) {
          return;
        }
        yield [n1.value, n2.value] as [T, S];
      }
    });
  }

  private constructor(private iter: Iterable<T>) {}
}
