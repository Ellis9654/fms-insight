/* Copyright (c) 2018, John Lenz

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of John Lenz, Black Maple Software, SeedTactics,
      nor the names of other contributors may be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

import * as reactRedux from 'react-redux';

type RemoveTypeProp<P> = P extends "type" ? never : P;
type RemoveType<A> = { [P in RemoveTypeProp<keyof A>]: A[P] };
export type GetActionTypes<A> = A extends {type: infer T} ? T : never;
export type ActionPayload<A, T> = A extends {type: T} ? RemoveType<A> : never;
export type DispatchFn<Args> =
  {} extends Args
  ? () => void
  : (payload: Args) => void;
export type DispatchAction<A, T> = DispatchFn<ActionPayload<A, T>>;

// Specialized type for connect
export type ActionCreator<A, Args> = (args: Args) => A;
export type ActionCreatorToDispatch<A, Creators> = {
  [P in keyof Creators]:
    Creators[P] extends ActionCreator<A, infer Args> ? DispatchFn<Args> :
    never;
};

// react-redux 6.0 has wierd bug around InferableComponentEnhancerWithProps
// in its attempt to support decorators.
// e.g. https://github.com/DefinitelyTyped/DefinitelyTyped/issues/25874
// so copy one without decorator support here for now.
export interface InferableComponentEnhancerWithProps<TInjectedProps, TNeedsProps> {
    <P extends TInjectedProps>(component: React.ComponentType<P>):
        React.ComponentClass<reactRedux.Omit<P, keyof TInjectedProps> & TNeedsProps>
     & {WrappedComponent: React.ComponentType<P>}
   ;
}

export interface Connect<A, S> {
  <P, TOwnProps = {}>(getProps: (s: S) => P):
    InferableComponentEnhancerWithProps<P, TOwnProps>;

  <P, TOwnProps = {}>(getProps: (s: S, ownProps: TOwnProps) => P):
    InferableComponentEnhancerWithProps<P, TOwnProps>;

  <P, Creators, TOwnProps = {}>(getProps: (s: S) => P, actionCreators: Creators):
    InferableComponentEnhancerWithProps<P & ActionCreatorToDispatch<A, Creators>, TOwnProps>;
}