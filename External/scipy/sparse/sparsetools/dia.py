# This file was automatically generated by SWIG (http://www.swig.org).
# Version 2.0.7
#
# Do not make changes to this file unless you know what you are doing--modify
# the SWIG interface file instead.



from sys import version_info
if version_info >= (2,6,0):
    def swig_import_helper():
        from os.path import dirname
        import imp
        fp = None
        try:
            fp, pathname, description = imp.find_module('_dia', [dirname(__file__)])
        except ImportError:
            import _dia
            return _dia
        if fp is not None:
            try:
                _mod = imp.load_module('_dia', fp, pathname, description)
            finally:
                fp.close()
            return _mod
    _dia = swig_import_helper()
    del swig_import_helper
else:
    import _dia
del version_info
try:
    _swig_property = property
except NameError:
    pass # Python < 2.2 doesn't have 'property'.
def _swig_setattr_nondynamic(self,class_type,name,value,static=1):
    if (name == "thisown"): return self.this.own(value)
    if (name == "this"):
        if type(value).__name__ == 'SwigPyObject':
            self.__dict__[name] = value
            return
    method = class_type.__swig_setmethods__.get(name,None)
    if method: return method(self,value)
    if (not static):
        self.__dict__[name] = value
    else:
        raise AttributeError("You cannot add attributes to %s" % self)

def _swig_setattr(self,class_type,name,value):
    return _swig_setattr_nondynamic(self,class_type,name,value,0)

def _swig_getattr(self,class_type,name):
    if (name == "thisown"): return self.this.own()
    method = class_type.__swig_getmethods__.get(name,None)
    if method: return method(self)
    raise AttributeError(name)

def _swig_repr(self):
    try: strthis = "proxy of " + self.this.__repr__()
    except: strthis = ""
    return "<%s.%s; %s >" % (self.__class__.__module__, self.__class__.__name__, strthis,)

try:
    _object = object
    _newclass = 1
except AttributeError:
    class _object : pass
    _newclass = 0



def dia_matvec(*args):
  """
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        signed char const [] diags, signed char const [] Xx, signed char [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        unsigned char const [] diags, unsigned char const [] Xx, unsigned char [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        short const [] diags, short const [] Xx, short [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        unsigned short const [] diags, unsigned short const [] Xx, unsigned short [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        int const [] diags, int const [] Xx, int [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        unsigned int const [] diags, unsigned int const [] Xx, unsigned int [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        long long const [] diags, long long const [] Xx, long long [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        unsigned long long const [] diags, unsigned long long const [] Xx, unsigned long long [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        float const [] diags, float const [] Xx, float [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        double const [] diags, double const [] Xx, double [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        long double const [] diags, long double const [] Xx, long double [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        npy_cfloat_wrapper const [] diags, npy_cfloat_wrapper const [] Xx, npy_cfloat_wrapper [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        npy_cdouble_wrapper const [] diags, npy_cdouble_wrapper const [] Xx, 
        npy_cdouble_wrapper [] Yx)
    dia_matvec(int const n_row, int const n_col, int const n_diags, int const L, int const [] offsets, 
        npy_clongdouble_wrapper const [] diags, npy_clongdouble_wrapper const [] Xx, 
        npy_clongdouble_wrapper [] Yx)
    """
  return _dia.dia_matvec(*args)
# This file is compatible with both classic and new-style classes.


