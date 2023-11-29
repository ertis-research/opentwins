import React, { Fragment } from 'react';
import CustomNavbar from './customNavbar';
import MainPart from './mainPart';
import KeyFeatures from './keyFeatures';
import UseCases from './useCases';
import Technologies from './technologies';

export default function Homepage(): JSX.Element {
  return (
    <Fragment>
      <CustomNavbar/>
      <MainPart/>
    </Fragment>
  );
}

/*
      <KeyFeatures/>
      <UseCases/>
      <Technologies/>
*/